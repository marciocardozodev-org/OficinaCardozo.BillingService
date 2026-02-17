using System;

namespace OficinaCardozo.BillingService.Messaging
{
    public class InboxMessage
    {
        public Guid Id { get; set; }
        public string EventType { get; set; }
        public string Payload { get; set; }
        public DateTime ReceivedAt { get; set; }
        public Guid ProviderEventId { get; set; }
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
    }
}
