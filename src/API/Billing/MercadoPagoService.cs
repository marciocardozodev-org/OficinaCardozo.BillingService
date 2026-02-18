using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    public class MercadoPagoService
    {
        public async Task<string> CreatePaymentPreferenceAsync(decimal amount, string osId)
        {
            // Mock: criar preferência de pagamento
            return "mock_preference_id";
        }

        public async Task<bool> ConfirmPaymentAsync(string providerPaymentId)
        {
            // Mock: confirmar pagamento
            return true;
        }

        public async Task<bool> ReversePaymentAsync(string providerPaymentId)
        {
            // Mock: estornar/cancelar pagamento
            return true;
        }
    }
}
