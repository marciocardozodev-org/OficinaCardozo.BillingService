using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class PaymentFailed
    {
        public Guid OsId { get; set; }
        public Guid PaymentId { get; set; }
        public PaymentStatus Status { get; set; }
        public string Reason { get; set; }
    }
}
