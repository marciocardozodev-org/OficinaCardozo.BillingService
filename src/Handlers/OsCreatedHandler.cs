using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;
using OFICINACARDOZO.BILLINGSERVICE;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace OFICINACARDOZO.BILLINGSERVICE.Handlers
{
    public class OsCreatedHandler
    {
        private readonly BillingDbContext _db;
        private readonly OrcamentoService _orcamentoService;
        private readonly PagamentoService _pagamentoService;
        private readonly ILogger<OsCreatedHandler> _logger;

        public OsCreatedHandler(
            BillingDbContext db, 
            OrcamentoService orcamentoService,
            PagamentoService pagamentoService,
            ILogger<OsCreatedHandler> logger)
        {
            _db = db;
            _orcamentoService = orcamentoService;
            _pagamentoService = pagamentoService;
            _logger = logger;
        }

        public async Task HandleAsync(EventEnvelope<OsCreated> envelope)
        {
            try
            {
                _logger.LogInformation(
                    "🎉 BillingService consumiu evento OsCreated. OsId: {OsId}, CorrelationId: {CorrelationId}",
                    envelope.Payload.OsId,
                    envelope.CorrelationId);

                var orcamento = await _orcamentoService.GetBudgetByOsIdAsync(envelope.Payload.OsId);
                if (orcamento == null)
                {
                    // ✅ TRANSACTIONAL OUTBOX PATTERN - FASE 1
                    // Criar Orçamento + OutboxMessage em UMA transação
                    
                    // ✅ Extração do valor do evento com fallback
                    const decimal DefaultBudgetAmount = 100.00m;
                    decimal budgetAmount;
                    bool usedFallback = false;
                    
                    if (envelope.Payload.Valor.HasValue && envelope.Payload.Valor.Value > 0)
                    {
                        budgetAmount = envelope.Payload.Valor.Value;
                        _logger.LogInformation(
                            "[CorrelationId: {CorrelationId}] Usando valor do evento OsCreated: {Valor} para OS {OsId}",
                            envelope.CorrelationId,
                            budgetAmount,
                            envelope.Payload.OsId);
                    }
                    else
                    {
                        budgetAmount = DefaultBudgetAmount;
                        usedFallback = true;
                        _logger.LogWarning(
                            "[CorrelationId: {CorrelationId}] Valor não fornecido ou inválido no OsCreated (Valor={ValorRecebido}). " +
                            "Usando fallback: {DefaultValue} para OS {OsId}",
                            envelope.CorrelationId,
                            envelope.Payload.Valor,
                            DefaultBudgetAmount,
                            envelope.Payload.OsId);
                    }
                    
                    orcamento = await _orcamentoService.GerarEEnviarOrcamentoAsync(
                        envelope.Payload.OsId,
                        budgetAmount,
                        "client@example.com",
                        envelope.CorrelationId,
                        envelope.CausationId
                    );

                    _logger.LogInformation(
                        "[CorrelationId: {CorrelationId}] Orçamento criado com ID {OrcamentoId} para OS {OsId}. " +
                        "Valor={Valor}, UsedFallback={UsedFallback}",
                        envelope.CorrelationId,
                        orcamento.Id,
                        envelope.Payload.OsId,
                        budgetAmount,
                        usedFallback);

                    var budgetGenerated = new BudgetGenerated
                    {
                        OsId = envelope.Payload.OsId,
                        BudgetId = Guid.NewGuid(),
                        Amount = budgetAmount,
                        Status = BudgetStatus.Generated
                    };

                    var outboxMessage = new OutboxMessage
                    {
                        AggregateId = orcamento.OsId,
                        AggregateType = "OrderService",
                        EventType = nameof(BudgetGenerated),
                        Payload = JsonSerializer.Serialize(budgetGenerated),
                        CreatedAt = DateTime.UtcNow,
                        Published = false,
                        CorrelationId = envelope.CorrelationId,
                        CausationId = Guid.NewGuid()
                    };

                    _db.Set<OutboxMessage>().Add(outboxMessage);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation(
                        "✅ BillingService gerou OutboxMessage para evento {EventType}. " +
                        "MessageId: {MessageId}, OsId: {OsId}, CorrelationId: {CorrelationId}, Status: ProntoParaPublicar",
                        outboxMessage.EventType,
                        outboxMessage.Id,
                        envelope.Payload.OsId,
                        envelope.CorrelationId);
                }
                else
                {
                    _logger.LogInformation(
                        "Orcamento ja existe para OS {OsId} (Id={OrcamentoId}). Reprocessando fluxo.",
                        envelope.Payload.OsId,
                        orcamento.Id);
                }

                await ProcessAutoFlowAsync(orcamento, envelope);
            }
            catch (Exception ex)
            {
                // ✅ IDEMPOTENCIA: Se orcamento ja existe, ignorar (duplicate key)
                if (IsDuplicateKey(ex))
                {
                    _logger.LogInformation(
                        "Orçamento para OS {OsId} já existe. Reprocessando fluxo automático.",
                        envelope.Payload.OsId);
                    var existing = await _orcamentoService.GetBudgetByOsIdAsync(envelope.Payload.OsId);
                    if (existing != null)
                    {
                        await ProcessAutoFlowAsync(existing, envelope);
                    }
                    return;  // ✅ Não relança - tratar como sucesso
                }
                
                _logger.LogError(
                    ex,
                    "❌ Erro ao processar OsCreated. OsId: {OsId}, CorrelationId: {CorrelationId}, Erro: {ErrorMessage}",
                    envelope.Payload.OsId,
                    envelope.CorrelationId,
                    ex.Message);
                throw;  // ❌ Relança apenas para erros reais
            }
        }

        private static bool IsDuplicateKey(Exception ex)
        {
            if (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }

            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("orcamento_os_id_key", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ProcessAutoFlowAsync(Orcamento orcamento, EventEnvelope<OsCreated> envelope)
        {
            if (orcamento.Status == StatusOrcamento.Enviado)
            {
                orcamento = await _orcamentoService.AprovaBudgetAsync(
                    orcamento.OsId,
                    envelope.CorrelationId,
                    Guid.NewGuid());

                _logger.LogInformation(
                    "Orcamento aprovado automaticamente para OS {OsId} (Id={OrcamentoId}).",
                    orcamento.OsId,
                    orcamento.Id);
            }

            if (orcamento.Status != StatusOrcamento.Aprovado)
            {
                _logger.LogInformation(
                    "Pagamento nao iniciado: orcamento OS {OsId} com status {Status}.",
                    orcamento.OsId,
                    orcamento.Status);
                return;
            }

            _logger.LogInformation(
                "[CorrelationId: {CorrelationId}] Iniciando pagamento para OS {OsId}. " +
                "Valor do orçamento: {ValorOrcamento}",
                envelope.CorrelationId,
                orcamento.OsId,
                orcamento.Valor);
                
            await _pagamentoService.IniciarPagamentoAsync(
                orcamento.OsId,
                orcamento.Id,
                orcamento.Valor,
                envelope.CorrelationId,
                Guid.NewGuid());
        }
    }
}
