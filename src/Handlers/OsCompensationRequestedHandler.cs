using System.Threading.Tasks;
using OficinaCardozo.BillingService.Contracts.Events;
using OficinaCardozo.BillingService.Messaging;

namespace OficinaCardozo.BillingService.Handlers
{
    public class OsCompensationRequestedHandler
    {
        public async Task HandleAsync(EventEnvelope<OsCompensationRequested> envelope)
        {
            // Compensação: cancelar/estornar pagamento, publicar PaymentReversed
        }
    }
}
