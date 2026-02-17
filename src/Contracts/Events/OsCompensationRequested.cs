using System;

namespace OficinaCardozo.BillingService.Contracts.Events
{
    public class OsCompensationRequested
    {
        public Guid OsId { get; set; }
        public string Reason { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}
