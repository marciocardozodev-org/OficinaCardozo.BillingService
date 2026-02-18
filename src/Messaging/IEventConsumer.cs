using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    public interface IEventConsumer
    {
        Task ConsumeAsync(string eventType, string payload, string correlationId, string causationId);
    }
}
