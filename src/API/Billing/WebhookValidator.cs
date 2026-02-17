using System.Threading.Tasks;

namespace OficinaCardozo.BillingService.API.Billing
{
    public class WebhookValidator
    {
        public async Task<bool> IsValidAsync(string providerEventId)
        {
            // Validar idempotência: verificar se providerEventId já foi processado
            return true;
        }
    }
}
