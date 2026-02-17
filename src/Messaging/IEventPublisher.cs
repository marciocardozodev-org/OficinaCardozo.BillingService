using System.Threading.Tasks;

namespace OficinaCardozo.BillingService.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(Contracts.Events.EventEnvelope<T> envelope);
    }
}
