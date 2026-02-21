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

            var paymentMethod = "PIX";

            // 1. Criar registro de pagamento em estado PENDENTE
            var pagamento = new Pagamento
            {
                OsId = osId,
                OrcamentoId = orcamentoId,
                Valor = valor,
                Metodo = paymentMethod,
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

            // 1.5 Criar evento PaymentPending para ser publicado em SNS
            var paymentPendingEvent = new PaymentPending
            {
                PaymentId = LongToGuid(pagamento.Id),
                OsId = osId,
                Status = PaymentStatus.Pending,
                Amount = valor,
                ProviderPaymentId = "", // Ainda não temos do provider
                PaymentMethod = paymentMethod
            };

            var pendingEventCausationId = Guid.NewGuid();

            var pendingOutboxMessage = new OutboxMessage
            {
                AggregateId = osId,
                AggregateType = "OrderService",
                EventType = nameof(PaymentPending),
                Payload = JsonSerializer.Serialize(paymentPendingEvent),
                CreatedAt = DateTime.UtcNow,
                Published = false,
                CorrelationId = correlationId,
                CausationId = pendingEventCausationId
            };

            _context.Set<OutboxMessage>().Add(pendingOutboxMessage);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Evento PaymentPending criado e enfileirado para publicação. CorrelationId: {CorrelationId}",
                correlationId);

            // 2. Chamar serviço de pagamento (Mercado Pago real ou mock)
            var providerPaymentId = await _mercadoPago.InitiatePaymentAsync(
                osId,
                orcamentoId,
                valor,
                paymentMethod,
                $"Pagamento PIX para OS {osId}");

            // 3. Atualizar o registro com resultado
            if (!string.IsNullOrEmpty(providerPaymentId))
            {
                // ⏳ Para PIX: Registrar ProviderPaymentId mas MANTER em Pendente
                // A confirmação virá via webhook do Mercado Pago
                pagamento.ProviderPaymentId = providerPaymentId;
                pagamento.AtualizadoEm = DateTime.UtcNow;
                // ⚠️ Status permanece Pendente até webhook confirmar
                _context.Pagamentos.Update(pagamento);

                _logger.LogInformation(
                    "Pagamento registrado como Pendente. ProviderPaymentId: {PaymentId}, aguardando webhook",
                    providerPaymentId);
            }
            else
            {
                // ❌ Falha ao criar pagamento no Mercado Pago
                pagamento.Status = StatusPagamento.Falhou;
                pagamento.AtualizadoEm = DateTime.UtcNow;
                _context.Pagamentos.Update(pagamento);

                // Criar evento PaymentFailed
                var paymentFailedEvent = new PaymentFailed
                {
                    PaymentId = LongToGuid(pagamento.Id),
                    OsId = osId,
                    Status = PaymentStatus.Failed,
                    Reason = "Falha ao gerar QR Code PIX no Mercado Pago"
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
                    "Falha ao gerar QR Code PIX para OS {OsId}. PaymentFailed enfileirado",
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
                Status = StatusPagamento.Pendente,  // ✅ Manter como Pendente, não Confirmado
                CorrelationId = correlationId,
                CausationId = causationId,
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            };
            _context.Pagamentos.Add(pagamento);
            _context.SaveChanges();

            // ✅ Criar evento PaymentPending para publicação em SNS
            var paymentPendingEvent = new PaymentPending
            {
                PaymentId = LongToGuid(pagamento.Id),
                OsId = osId,
                Status = PaymentStatus.Pending,
                Amount = valor,
                ProviderPaymentId = "", // Vazio inicialmente
                PaymentMethod = metodo
            };

            var outboxMessage = new OutboxMessage
            {
                AggregateId = osId,
                AggregateType = "OrderService",
                EventType = nameof(PaymentPending),
                Payload = JsonSerializer.Serialize(paymentPendingEvent),
                CreatedAt = DateTime.UtcNow,
                Published = false,
                CorrelationId = correlationId,
                CausationId = causationId
            };

            _context.Set<OutboxMessage>().Add(outboxMessage);
            _context.SaveChanges();

            _logger.LogInformation(
                "Pagamento {PaymentId} registrado como Pendente. PaymentPending event enfileirado para publicação.",
                pagamento.Id);

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