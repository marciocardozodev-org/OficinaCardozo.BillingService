using System.Threading.Tasks;

namespace OficinaCardozo.BillingService.Messaging
{
    public class OutboxProcessor
    {
        private readonly IEventPublisher _publisher;
        // Injeção do DbContext e Outbox

        public OutboxProcessor(IEventPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task ProcessPendingMessagesAsync()
        {
            // Buscar mensagens não publicadas, publicar e marcar como publicadas
        }
    }
}
