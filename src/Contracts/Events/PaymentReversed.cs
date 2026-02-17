using System;

namespace OficinaCardozo.BillingService.Contracts.Events
{
    public class PaymentReversed
    {
        public Guid OsId { get; set; }
        public Guid PaymentId { get; set; }
        public PaymentStatus Status { get; set; }
        public string Reason { get; set; }
    }
}
