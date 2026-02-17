using System.Threading.Tasks;

namespace OficinaCardozo.BillingService.API.Billing
{
    public class IdempotencyService
    {
        public async Task<bool> IsDuplicateAsync(string providerEventId)
        {
            // Mock: verificar duplicidade
            return false;
        }
    }
}
