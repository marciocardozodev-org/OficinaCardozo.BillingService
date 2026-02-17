using System.Threading.Tasks;
using OficinaCardozo.BillingService.Contracts.Events;
using OficinaCardozo.BillingService.Messaging;

namespace OficinaCardozo.BillingService.Handlers
{
    public class OsCanceledHandler
    {
        public async Task HandleAsync(EventEnvelope<OsCanceled> envelope)
        {
            // Compensação: cancelar/estornar pagamento, publicar PaymentReversed/PaymentCanceled
        }
    }
}
