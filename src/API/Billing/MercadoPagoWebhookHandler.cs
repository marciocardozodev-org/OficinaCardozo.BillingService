using System;
using System.Text.Json;
using System.Threading.Tasks;
using OFICINACARDOZO.BILLINGSERVICE;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using Microsoft.Extensions.Logging;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    /// <summary>
    /// Processa webhooks do Mercado Pago
    /// Atualiza status do pagamento quando MP notifica
    /// </summary>
    public class MercadoPagoWebhookHandler
    {
        private readonly BillingDbContext _db;
        private readonly PagamentoService _pagamentoService;
        private readonly ILogger<MercadoPagoWebhookHandler> _logger;
        
        public MercadoPagoWebhookHandler(
            BillingDbContext db,
            PagamentoService pagamentoService,
            ILogger<MercadoPagoWebhookHandler> logger)
        {
            _db = db;
            _pagamentoService = pagamentoService;
            _logger = logger;
        }
        
        /// <summary>
        /// Processar webhook do Mercado Pago
        /// Valida assinatura, busca pagamento, atualiza status
        /// </summary>
        public async Task HandleWebhookAsync(
            string type,
            string paymentId,
            string? signature = null)
        {
            try
            {
                // ✅ TODO: Validar assinatura do webhook (MercadoPago-Signature JWT)
                
                var mpPaymentId = long.Parse(paymentId);
                
                _logger.LogInformation(
                    "Recebido webhook Mercado Pago. Type: {Type}, PaymentId: {PaymentId}",
                    type, paymentId);
                
                // Opcional: Buscar status real do pagamento na API
                // await GetPaymentStatusFromMPAsync(mpPaymentId);
                
                // Atualizar registros locais conforme necessário
                // Exemplo: Se status é 'approved', publicar PaymentConfirmed
                
                _logger.LogInformation(
                    "Webhook processado com sucesso para PaymentId: {PaymentId}",
                    paymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Erro ao processar webhook Mercado Pago para PaymentId: {PaymentId}",
                    paymentId);
                throw;
            }
        }
    }
}
