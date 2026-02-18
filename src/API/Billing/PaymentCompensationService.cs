using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    public class PaymentCompensationService
    {
        public async Task<bool> CompensateAsync(string providerPaymentId)
        {
            // Mock: compensação de pagamento (cancelamento/estorno)
            return true;
        }
    }
}
