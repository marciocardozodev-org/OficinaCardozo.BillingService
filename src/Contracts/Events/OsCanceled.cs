using System;

namespace OficinaCardozo.BillingService.Contracts.Events
{
    public class OsCanceled
    {
        public Guid OsId { get; set; }
        public string Reason { get; set; }
        public DateTime CanceledAt { get; set; }
    }
}
