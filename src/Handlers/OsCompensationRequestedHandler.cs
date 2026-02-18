using System.Threading.Tasks;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;

namespace OFICINACARDOZO.BILLINGSERVICE.Handlers
{
    public class OsCompensationRequestedHandler
    {
        public async Task HandleAsync(EventEnvelope<OsCompensationRequested> envelope)
        {
            // Compensação: cancelar/estornar pagamento, publicar PaymentReversed
        }
    }
}
