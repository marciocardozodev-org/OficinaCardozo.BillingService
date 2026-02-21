using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    public class MercadoPagoService : IMercadoPagoService
    {
        private readonly ILogger<MercadoPagoService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        private const string SandboxBaseUrl = "https://api.sandbox.mercadopago.com";
        private const string ProductionBaseUrl = "https://api.mercadopago.com";

        public MercadoPagoService(
            ILogger<MercadoPagoService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient("MercadoPago");
        }

        /// <summary>
        /// Inicia pagamento na API real do Mercado Pago
        /// </summary>
        public async Task<string?> InitiatePaymentAsync(
            Guid osId,
            long orcamentoId,
            decimal valor,
            string metodo = "CREDIT_CARD",
            string? description = null)
        {
            try
            {
                var accessToken = _configuration["MERCADOPAGO_ACCESS_TOKEN"];
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("MercadoPago AccessToken não configurado. Retornando null.");
                    return null;
                }

                var isSandbox = _configuration.GetValue<bool>("MERCADOPAGO_IS_SANDBOX", true);
                var baseUrl = isSandbox ? SandboxBaseUrl : ProductionBaseUrl;

                _logger.LogInformation(
                    "Iniciando pagamento (HTTP) para OS {OsId}, Valor: {Valor}, Ambiente: {Ambiente}",
                    osId, valor, isSandbox ? "SANDBOX" : "PRODUCAO");

                var paymentMethodId = MapMetodoToPagamentoMP(metodo);
                
                // Para PIX, não precisa de token. Para outros métodos (cartão), precisa
                var paymentRequest = paymentMethodId == "pix" ? new
                {
                    transaction_amount = valor,
                    description = description ?? $"Pagamento PIX para OS {osId}",
                    payment_method_id = paymentMethodId,
                    payer = new
                    {
                        email = _configuration["MERCADOPAGO_TEST_EMAIL"] ?? "test@example.com"
                    },
                    installments = 1,
                    external_reference = osId.ToString(),
                    metadata = new
                    {
                        osId = osId.ToString(),
                        orcamentoId = orcamentoId
                    }
                } : new
                {
                    transaction_amount = valor,
                    description = description ?? $"Pagamento para OS {osId}",
                    payer = new
                    {
                        email = _configuration["MERCADOPAGO_TEST_EMAIL"] ?? "test@example.com"
                    },
                    payment_method_id = paymentMethodId,
                    token = _configuration["MERCADOPAGO_TEST_CARD_TOKEN"] ?? "",
                    installments = 1,
                    external_reference = osId.ToString(),
                    metadata = new
                    {
                        osId = osId.ToString(),
                        orcamentoId = orcamentoId
                    }
                };

                var jsonContent = JsonSerializer.Serialize(paymentRequest);
                
                // Log para debug
                var tokenValue = _configuration["MERCADOPAGO_TEST_CARD_TOKEN"];
                _logger.LogInformation(
                    "Token configurado: {TokenLength} caracteres (vazio={isEmpty}), Email: {Email}",
                    tokenValue?.Length ?? 0,
                    string.IsNullOrEmpty(tokenValue),
                    _configuration["MERCADOPAGO_TEST_EMAIL"]);

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                _httpClient.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

                var response = await _httpClient.PostAsync($"{baseUrl}/v1/payments", httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Erro ao criar pagamento no Mercado Pago. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, responseContent);
                    return null;
                }

                using var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;
                var paymentId = root.GetProperty("id").GetInt64();
                var paymentStatus = root.GetProperty("status").GetString();

                _logger.LogInformation(
                    "Pagamento criado no Mercado Pago. ID: {PaymentId}, Status: {Status}, OsId: {OsId}",
                    paymentId, paymentStatus, osId);

                return paymentId.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exceção ao criar pagamento no Mercado Pago para OS {OsId}",
                    osId);
                return null;
            }
        }

        /// <summary>
        /// Mapear método de pagamento interno para nomeação do Mercado Pago
        /// </summary>
        private string MapMetodoToPagamentoMP(string metodo)
        {
            return metodo.ToUpper() switch
            {
                "CREDIT_CARD" or "CREDITO" => "visa",  // Default para testes
                "DEBIT_CARD" or "DEBITO" => "debit_card",
                "BOLETO" => "boleto",
                "PIX" => "pix",
                _ => "visa"
            };
        }
    }
}
