using System.Threading.Tasks;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;

namespace OFICINACARDOZO.BILLINGSERVICE.Handlers
{
    public class OsCanceledHandler
    {
        public async Task HandleAsync(EventEnvelope<OsCanceled> envelope)
        {
            // Compensação: cancelar/estornar pagamento, publicar PaymentReversed/PaymentCanceled
        }
    }
}
