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
                Metodo = "CREDIT_CARD",
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

            // 2. Chamar serviço de pagamento (Mercado Pago real ou mock)
            var providerPaymentId = await _mercadoPago.InitiatePaymentAsync(
                osId,
                orcamentoId,
                valor,
                "CREDIT_CARD",
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
                    PaymentId = LongToGuid(pagamento.Id),
                    OsId = osId,
                    Status = PaymentStatus.Confirmed,
                    Amount = valor,
                    ProviderPaymentId = providerPaymentId
                };

                var eventCausationId = Guid.NewGuid();

                var outboxMessage = new OutboxMessage
                {
                    AggregateId = osId,
                    AggregateType = "OrderService",
                    EventType = nameof(PaymentConfirmed),
                    Payload = JsonSerializer.Serialize(paymentConfirmedEvent),
                    CreatedAt = DateTime.UtcNow,
                    Published = false,
                    CorrelationId = correlationId,
                    CausationId = eventCausationId
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
                    PaymentId = LongToGuid(pagamento.Id),
                    OsId = osId,
                    Status = PaymentStatus.Failed,
                    Reason = "Falha na autorização do provedor"
                };

                var eventCausationId = Guid.NewGuid();

                var outboxMessage = new OutboxMessage
                {
                    AggregateId = osId,
                    AggregateType = "OrderService",
                    EventType = nameof(PaymentFailed),
                    Payload = JsonSerializer.Serialize(paymentFailedEvent),
                    CreatedAt = DateTime.UtcNow,
                    Published = false,
                    CorrelationId = correlationId,
                    CausationId = eventCausationId
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

        private static Guid LongToGuid(long value)
        {
            var bytes = new byte[16];
            var valueBytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(valueBytes);
            }

            Array.Copy(valueBytes, 0, bytes, 8, 8);
            return new Guid(bytes);
        }

        public Pagamento? ObterPagamento(long pagamentoId)
        {
            return _context.Pagamentos.FirstOrDefault(p => p.Id == pagamentoId);
        }
    }
}