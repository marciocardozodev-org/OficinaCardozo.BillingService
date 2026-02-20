using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    /// <summary>
    /// Mock de integração com Mercado Pago para testes
    /// Em produção, seria substituído por implementação real da API
    /// </summary>
    public class MercadoPagoMockService : IMercadoPagoService
    {
        private readonly ILogger<MercadoPagoMockService> _logger;
        private static Random _random = new Random();

        public MercadoPagoMockService(ILogger<MercadoPagoMockService> logger)
        {
            _logger = logger;
        }

        public async Task<string?> InitiatePaymentAsync(
            Guid osId,
            long orcamentoId,
            decimal valor,
            string metodo = "CREDIT_CARD",
            string? description = null)
        {
            // Simular delay de API call
            await Task.Delay(100);

            // Mock: gerar ID único de pagamento (como se fosse do Mercado Pago)
            string providerPaymentId = $"MP-{osId:N}-{orcamentoId}-{DateTime.UtcNow.Ticks}";

            _logger.LogInformation(
                "💳 [MOCK] Pagamento iniciado. OsId: {OsId}, Valor: {Valor}, ProviderPaymentId: {PaymentId}",
                osId, valor, providerPaymentId);

            // Mock: 95% de chance de sucesso, 5% de falha
            if (_random.Next(100) >= 95)
            {
                _logger.LogWarning(
                    "❌ [MOCK] Simulação de falha de pagamento para OsId: {OsId}",
                    osId);
                return null;  // Simula falha
            }

            _logger.LogInformation(
                "✅ [MOCK] Pagamento autorizado. ProviderPaymentId: {PaymentId}",
                providerPaymentId);

            return providerPaymentId;
        }
    }
}
