using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using OFICINACARDOZO.BILLINGSERVICE;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        private readonly ILogger<MercadoPagoWebhookHandler> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private const string SandboxBaseUrl = "https://api.sandbox.mercadopago.com";
        private const string ProductionBaseUrl = "https://api.mercadopago.com";
        
        public MercadoPagoWebhookHandler(
            BillingDbContext db,
            ILogger<MercadoPagoWebhookHandler> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient("MercadoPago");
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
                if (!string.Equals(type, "payment", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Webhook ignorado. Tipo nao suportado: {Type}",
                        type);
                    return;
                }

                if (string.IsNullOrWhiteSpace(paymentId))
                {
                    _logger.LogWarning("Webhook sem paymentId. Ignorando.");
                    return;
                }

                // TODO: validar assinatura do webhook, se configurado
                
                var mpPaymentId = long.Parse(paymentId);
                
                _logger.LogInformation(
                    "Recebido webhook Mercado Pago. Type: {Type}, PaymentId: {PaymentId}",
                    type, paymentId);

                var mpPayment = await GetPaymentFromMpAsync(mpPaymentId);
                if (mpPayment == null)
                {
                    _logger.LogWarning(
                        "Nao foi possivel obter status no Mercado Pago para PaymentId: {PaymentId}",
                        paymentId);
                    return;
                }

                var pagamento = await _db.Pagamentos
                    .FirstOrDefaultAsync(p => p.ProviderPaymentId == paymentId);

                if (pagamento == null && Guid.TryParse(mpPayment.OsId, out var osIdFromMp))
                {
                    pagamento = await _db.Pagamentos
                        .FirstOrDefaultAsync(p => p.OsId == osIdFromMp);
                }

                if (pagamento == null)
                {
                    _logger.LogWarning(
                        "Pagamento local nao encontrado para PaymentId: {PaymentId}",
                        paymentId);
                    return;
                }

                var statusAtual = pagamento.Status;
                var novoStatus = MapMpStatus(mpPayment.Status);

                if (novoStatus == StatusPagamento.Pendente)
                {
                    _logger.LogInformation(
                        "Pagamento ainda pendente no provedor. PaymentId: {PaymentId}",
                        paymentId);
                    return;
                }

                if (novoStatus == statusAtual)
                {
                    _logger.LogInformation(
                        "Pagamento ja esta no status {Status}. Nenhuma acao necessaria.",
                        statusAtual);
                    return;
                }

                pagamento.Status = novoStatus;
                pagamento.ProviderPaymentId = paymentId;
                pagamento.AtualizadoEm = DateTime.UtcNow;

                if (novoStatus == StatusPagamento.Confirmado)
                {
                    var paymentConfirmedEvent = new PaymentConfirmed
                    {
                        PaymentId = LongToGuid(pagamento.Id),
                        OsId = pagamento.OsId,
                        Status = PaymentStatus.Confirmed,
                        Amount = pagamento.Valor,
                        ProviderPaymentId = paymentId
                    };

                    _db.Set<OutboxMessage>().Add(new OutboxMessage
                    {
                        AggregateId = pagamento.OsId,
                        AggregateType = "OrderService",
                        EventType = nameof(PaymentConfirmed),
                        Payload = JsonSerializer.Serialize(paymentConfirmedEvent),
                        CreatedAt = DateTime.UtcNow,
                        Published = false,
                        CorrelationId = pagamento.CorrelationId,
                        CausationId = Guid.NewGuid()
                    });
                }
                else if (novoStatus == StatusPagamento.Falhou)
                {
                    var paymentFailedEvent = new PaymentFailed
                    {
                        PaymentId = LongToGuid(pagamento.Id),
                        OsId = pagamento.OsId,
                        Status = PaymentStatus.Failed,
                        Reason = "Pagamento nao aprovado pelo provedor"
                    };

                    _db.Set<OutboxMessage>().Add(new OutboxMessage
                    {
                        AggregateId = pagamento.OsId,
                        AggregateType = "OrderService",
                        EventType = nameof(PaymentFailed),
                        Payload = JsonSerializer.Serialize(paymentFailedEvent),
                        CreatedAt = DateTime.UtcNow,
                        Published = false,
                        CorrelationId = pagamento.CorrelationId,
                        CausationId = Guid.NewGuid()
                    });
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Webhook processado com sucesso. PaymentId: {PaymentId}, Status: {Status}",
                    paymentId, novoStatus);
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

        private async Task<MercadoPagoPayment?> GetPaymentFromMpAsync(long paymentId)
        {
            var accessToken = _configuration["MERCADOPAGO_ACCESS_TOKEN"];
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("AccessToken do MercadoPago nao configurado para webhook.");
                return null;
            }

            var isSandbox = _configuration.GetValue<bool>("MERCADOPAGO_IS_SANDBOX", true);
            var baseUrl = isSandbox ? SandboxBaseUrl : ProductionBaseUrl;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.GetAsync($"{baseUrl}/v1/payments/{paymentId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Erro ao consultar pagamento no Mercado Pago. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);
                return null;
            }

            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;

            var status = root.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? string.Empty
                : string.Empty;

            string? osId = null;
            if (root.TryGetProperty("metadata", out var metadata) &&
                metadata.TryGetProperty("osId", out var osIdProp))
            {
                osId = osIdProp.GetString();
            }

            return new MercadoPagoPayment
            {
                Status = status,
                OsId = osId
            };
        }

        private static StatusPagamento MapMpStatus(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                "approved" => StatusPagamento.Confirmado,
                "rejected" => StatusPagamento.Falhou,
                "cancelled" => StatusPagamento.Falhou,
                "refunded" => StatusPagamento.Falhou,
                "charged_back" => StatusPagamento.Falhou,
                _ => StatusPagamento.Pendente
            };
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

        private sealed class MercadoPagoPayment
        {
            public string Status { get; set; } = string.Empty;
            public string? OsId { get; set; }
        }
    }
}
