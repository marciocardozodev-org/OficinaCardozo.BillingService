using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    public class InboxMessage
    {
        public long Id { get; set; }
        public string EventType { get; set; }
        public string Payload { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string ProviderEventId { get; set; }
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
        public bool Processed { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
