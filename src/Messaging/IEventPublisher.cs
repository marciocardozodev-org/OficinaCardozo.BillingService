using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(Contracts.Events.EventEnvelope<T> envelope);
    }
}
