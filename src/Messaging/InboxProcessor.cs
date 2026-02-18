using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    public class InboxProcessor
    {
        // Injeção do DbContext e Inbox

        public InboxProcessor()
        {
        }

        public async Task ProcessIncomingMessageAsync(string eventType, string payload, string correlationId, string causationId)
        {
            // Dedup, salvar ProviderEventId, processar handler
        }
    }
}
