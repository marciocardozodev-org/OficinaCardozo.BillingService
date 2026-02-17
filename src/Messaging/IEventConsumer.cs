using System.Threading.Tasks;

namespace OficinaCardozo.BillingService.Messaging
{
    public interface IEventConsumer
    {
        Task ConsumeAsync(string eventType, string payload, string correlationId, string causationId);
    }
}
