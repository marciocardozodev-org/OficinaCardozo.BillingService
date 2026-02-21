using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    public class MercadoPagoService : IMercadoPagoService
    {
        private readonly ILogger<MercadoPagoService> _logger;
        private readonly IConfiguration _configuration;
        public MercadoPagoService(
            ILogger<MercadoPagoService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
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
                MercadoPago.SDK.SetAccessToken(accessToken);
                _logger.LogInformation(
                    "Iniciando pagamento (SDK) para OS {OsId}, Valor: {Valor}",
                    osId, valor);
                var payment = new MercadoPago.Resources.Payment()
                {
                    TransactionAmount = valor,
                    Description = description ?? $"Pagamento para OS {osId}",
                    PaymentMethodId = MapMetodoToPagamentoMP(metodo),
                    Installments = 1,
                    ExternalReference = osId.ToString(),
                    Payer = new MercadoPago.Resources.Payer()
                    {
                        Email = _configuration["MercadoPago:TestEmail"] ?? "test@example.com"
                    }
                };
                payment.Save();
                if (payment.Id == null)
                {
                    _logger.LogError("Erro ao criar pagamento no Mercado Pago via SDK. Status: {Status}, Detail: {Detail}", payment.Status, payment.StatusDetail);
                    return null;
                }
                _logger.LogInformation(
                    "Pagamento criado no Mercado Pago via SDK. ID: {PaymentId}, Status: {Status}, OsId: {OsId}",
                    payment.Id, payment.Status, osId);
                return payment.Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exceção ao criar pagamento no Mercado Pago via SDK para OS {OsId}",
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
