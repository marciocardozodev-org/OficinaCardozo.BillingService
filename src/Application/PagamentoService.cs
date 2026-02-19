using System.Text.Json;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using OFICINACARDOZO.BILLINGSERVICE.API.Billing;
using Microsoft.Extensions.Logging;

namespace OFICINACARDOZO.BILLINGSERVICE.Application
{
    public class PagamentoService
    {
        private readonly BillingDbContext _context;
        private readonly IMercadoPagoService _mercadoPago;
        private readonly ILogger<PagamentoService> _logger;

        public PagamentoService(
            BillingDbContext context,
            IMercadoPagoService mercadoPago,
            ILogger<PagamentoService> logger)
        {
            _context = context;
            _mercadoPago = mercadoPago;
            _logger = logger;
        }

        /// <summary>
        /// Inicia fluxo de pagamento: cria PaymentPending, chama provider, 
        /// e publica PaymentConfirmed/PaymentFailed via Outbox Pattern
        /// </summary>
        public async Task<Pagamento> IniciarPagamentoAsync(
            Guid osId,
            long orcamentoId,
            decimal valor,
            Guid correlationId,
            Guid causationId)
        {
            _logger.LogInformation(
                "Iniciando pagamento para OS {OsId}, Orçamento {OrcamentoId}, Valor: {Valor}",
                osId, orcamentoId, valor);

            // 1. Criar registro de pagamento em estado PENDENTE
            var pagamento = new Pagamento
            {
                OsId = osId,
                OrcamentoId = orcamentoId,
                Valor = valor,
                Metodo = "CREDITO_MOCK",
                Status = StatusPagamento.Pendente,
                CorrelationId = correlationId,
                CausationId = causationId,
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            };
            _context.Pagamentos.Add(pagamento);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Pagamento registrado com ID {PaymentId} em estado Pendente",
                pagamento.Id);

            // 2. Chamar serviço de pagamento (Mercado Pago mock)
            var providerPaymentId = await _mercadoPago.InitiatePaymentAsync(
                osId,
                orcamentoId,
                valor,
                "CREDITO_MOCK",
                $"Pagamento para OS {osId}");

            // 3. Atualizar o registro com resultado e publicar evento apropriado
            if (!string.IsNullOrEmpty(providerPaymentId))
            {
                // ✅ Pagamento confirmado
                pagamento.Status = StatusPagamento.Confirmado;
                pagamento.ProviderPaymentId = providerPaymentId;
                pagamento.AtualizadoEm = DateTime.UtcNow;
                _context.Pagamentos.Update(pagamento);

                // Criar evento PaymentConfirmed
                var paymentConfirmedEvent = new PaymentConfirmed
                {
                    PaymentId = pagamento.Id,
                    OsId = osId,
                    OrcamentoId = orcamentoId,
                    Valor = valor,
                    ProviderPaymentId = providerPaymentId,
                    Status = "Confirmado",
                    ConfirmedAt = DateTime.UtcNow,
                    CorrelationId = correlationId,
                    CausationId = Guid.NewGuid()  // Novo ID para o evento
                };

                var outboxMessage = new OutboxMessage
                {
                    AggregateId = osId,
                    AggregateType = "OrderService",
                    EventType = nameof(PaymentConfirmed),
                    Payload = JsonSerializer.Serialize(paymentConfirmedEvent),
                    CreatedAt = DateTime.UtcNow,
                    Published = false,
                    CorrelationId = correlationId,
                    CausationId = paymentConfirmedEvent.CausationId
                };

                _context.Set<OutboxMessage>().Add(outboxMessage);

                _logger.LogInformation(
                    "Pagamento confirmado. ProviderPaymentId: {PaymentId}, OutboxMessage criada",
                    providerPaymentId);
            }
            else
            {
                // ❌ Pagamento falhou
                pagamento.Status = StatusPagamento.Falhou;
                pagamento.AtualizadoEm = DateTime.UtcNow;
                _context.Pagamentos.Update(pagamento);

                // Criar evento PaymentFailed
                var paymentFailedEvent = new PaymentFailed
                {
                    PaymentId = pagamento.Id,
                    OsId = osId,
                    OrcamentoId = orcamentoId,
                    Valor = valor,
                    Reason = "Falha na autorização do provedor",
                    FailedAt = DateTime.UtcNow,
                    CorrelationId = correlationId,
                    CausationId = Guid.NewGuid()
                };

                var outboxMessage = new OutboxMessage
                {
                    AggregateId = osId,
                    AggregateType = "OrderService",
                    EventType = nameof(PaymentFailed),
                    Payload = JsonSerializer.Serialize(paymentFailedEvent),
                    CreatedAt = DateTime.UtcNow,
                    Published = false,
                    CorrelationId = correlationId,
                    CausationId = paymentFailedEvent.CausationId
                };

                _context.Set<OutboxMessage>().Add(outboxMessage);

                _logger.LogWarning(
                    "Pagamento falhou para OS {OsId}. PaymentFailed enfileirado",
                    osId);
            }

            await _context.SaveChangesAsync();

            return pagamento;
        }

        public Pagamento RegistrarPagamento(
            Guid osId, 
            long orcamentoId,
            decimal valor, 
            string metodo,
            Guid correlationId,
            Guid causationId)
        {
            var pagamento = new Pagamento
            {
                OsId = osId,
                OrcamentoId = orcamentoId,
                Valor = valor,
                Metodo = metodo,
                Status = StatusPagamento.Confirmado,
                CorrelationId = correlationId,
                CausationId = causationId,
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            };
            _context.Pagamentos.Add(pagamento);
            _context.SaveChanges();
            return pagamento;
        }

        public Pagamento? ObterPagamento(long pagamentoId)
        {
            return _context.Pagamentos.FirstOrDefault(p => p.Id == pagamentoId);
        }
    }
}