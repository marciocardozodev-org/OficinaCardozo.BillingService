using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Messaging
{
    public class OutboxMessage
    {
        public long Id { get; set; }
        public long AggregateId { get; set; }
        public string EventType { get; set; }
        public string Payload { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Published { get; set; }
        public DateTime? PublishedAt { get; set; }
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
    }
}
