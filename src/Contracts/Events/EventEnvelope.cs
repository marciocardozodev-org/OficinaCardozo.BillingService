using System;

namespace OficinaCardozo.BillingService.Contracts.Events
{
    public class EventEnvelope<T>
    {
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
        public DateTime Timestamp { get; set; }
        public T Payload { get; set; }
    }
}
