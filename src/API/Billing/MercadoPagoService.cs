using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    public class MercadoPagoService
        {
            /// <summary>
            /// Integração REAL com API de Pagamentos do Mercado Pago
            /// Produção: cria pagamento na API real
            /// Sandbox: ambiente de teste do Mercado Pago
            /// </summary>
            public class MercadoPagoService : IMercadoPagoService
            {
                private readonly HttpClient _httpClient;
                private readonly ILogger<MercadoPagoService> _logger;
                private readonly IConfiguration _configuration;
            
                // URLs Base
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
                        var accessToken = _configuration["MercadoPago:AccessToken"];
                        if (string.IsNullOrEmpty(accessToken))
                        {
                            _logger.LogWarning("MercadoPago AccessToken não configurado. Retornando null.");
                            return null;
                        }
                    
                        var isSandbox = _configuration.GetValue<bool>("MercadoPago:IsSandbox", true);
                        var baseUrl = isSandbox ? SandboxBaseUrl : ProductionBaseUrl;
                    
                        _logger.LogInformation(
                            "Iniciando pagamento (Environment: {Environment}) para OS {OsId}, Valor: {Valor}",
                            isSandbox ? "SANDBOX" : "PRODUCTION",
                            osId, valor);
                    
                        // Construir payload
                        var paymentRequest = new
                        {
                            transaction_amount = valor,
                            description = description ?? $"Pagamento para OS {osId}",
                            payer = new
                            {
                                email = _configuration["MercadoPago:TestEmail"] ?? "test@example.com"
                            },
                            payment_method_id = MapMetodoToPagamentoMP(metodo),
                            token = _configuration["MercadoPago:TestCardToken"] ?? "", // Para sandbox
                            installments = 1,
                            external_reference = osId.ToString(),
                            metadata = new
                            {
                                osId = osId.ToString(),
                                orcamentoId = orcamentoId
                            }
                        };
                    
                        // Serializar
                        var jsonContent = JsonSerializer.Serialize(paymentRequest);
                        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    
                        // Adicionar header Authorization
                        _httpClient.DefaultRequestHeaders.Clear();
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    
                        // Fazer request
                        var response = await _httpClient.PostAsync(
                            $"{baseUrl}/v1/payments",
                            httpContent);
                    
                        var responseContent = await response.Content.ReadAsStringAsync();
                    
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogError(
                                "Erro ao criar pagamento no Mercado Pago. Status: {StatusCode}, Response: {Response}",
                                response.StatusCode, responseContent);
                            return null;
                        }
                    
                        // Parsear resposta
                        using var jsonDoc = JsonDocument.Parse(responseContent);
                        var root = jsonDoc.RootElement;
                    
                        var paymentId = root.GetProperty("id").GetInt64();
                        var paymentStatus = root.GetProperty("status").GetString();
                    
                        _logger.LogInformation(
                            "Pagamento criado no Mercado Pago. ID: {PaymentId}, Status: {Status}, OsId: {OsId}",
                            paymentId, paymentStatus, osId);
                    
                        // Retornar ID como string para compatibilidade
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
}
