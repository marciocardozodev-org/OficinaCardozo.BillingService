using System.Text.Json;
using System.Threading.Tasks;
using OFICINACARDOZO.BILLINGSERVICE;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using Microsoft.Extensions.Logging;

namespace OFICINACARDOZO.BILLINGSERVICE.Handlers
{
    public class OsCreatedHandler
    {
        private readonly BillingDbContext _db;
        private readonly OrcamentoService _orcamentoService;
        private readonly ILogger<OsCreatedHandler> _logger;

        public OsCreatedHandler(
            BillingDbContext db, 
            OrcamentoService orcamentoService, 
            ILogger<OsCreatedHandler> logger)
        {
            _db = db;
            _orcamentoService = orcamentoService;
            _logger = logger;
        }

        public async Task HandleAsync(EventEnvelope<OsCreated> envelope)
        {
            try
            {
                _logger.LogInformation(
                    "Processando OsCreated para OS {OsId} com CorrelationId {CorrelationId}",
                    envelope.Payload.OsId,
                    envelope.CorrelationId);

                // ✅ TRANSACTIONAL OUTBOX PATTERN - FASE 1
                // Criar Orçamento + OutboxMessage em UMA transação
                
                // 1. Criar orçamento local (valor padrão)
                decimal budgetAmount = 100.00m;
                var orcamento = await _orcamentoService.GerarEEnviarOrcamentoAsync(
                    envelope.Payload.OsId,
                    budgetAmount,
                    "client@example.com",
                    envelope.CorrelationId,
                    envelope.CausationId
                );

                _logger.LogInformation(
                    "Orçamento criado com ID {OrcamentoId} para OS {OsId}",
                    orcamento.Id,
                    envelope.Payload.OsId);

                // 2. Criar evento de saída (BudgetGenerated)
                var budgetGenerated = new BudgetGenerated
                {
                    OsId = envelope.Payload.OsId,
                    BudgetId = Guid.NewGuid(),
                    Amount = budgetAmount,
                    Status = BudgetStatus.Generated
                };

                // 3. Criar OutboxMessage (NÃO PUBLICAMOS AGORA)
                var outboxMessage = new OutboxMessage
                {
                    AggregateId = orcamento.OsId,
                    AggregateType = "OrderService",
                    EventType = nameof(BudgetGenerated),
                    Payload = JsonSerializer.Serialize(budgetGenerated),
                    CreatedAt = DateTime.UtcNow,
                    Published = false,  // ✅ CRÍTICO: Não publicado ainda
                    CorrelationId = envelope.CorrelationId,
                    CausationId = Guid.NewGuid()  // Novo ID para BudgetGenerated
                };

                _db.Set<OutboxMessage>().Add(outboxMessage);

                // 4. Salvar tudo em transação ÚNICA
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "OutboxMessage criada com ID {MessageId} para evento {EventType}",
                    outboxMessage.Id,
                    outboxMessage.EventType);

                // ✅ PARAR AQUI!
                // OutboxProcessor (background job) vai publicar isso mais tarde
                // Isso garante resiliência: se publicação falhar, teremos retries automáticos
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erro ao processar OsCreated para OS {OsId}",
                    envelope.Payload.OsId);
                throw;
            }
        }
    }
}
