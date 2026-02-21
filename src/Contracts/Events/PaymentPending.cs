using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class PaymentPending
    {
        public Guid OsId { get; set; }
        public Guid PaymentId { get; set; }
        public PaymentStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string ProviderPaymentId { get; set; }
        public string PaymentMethod { get; set; }
    }
}
