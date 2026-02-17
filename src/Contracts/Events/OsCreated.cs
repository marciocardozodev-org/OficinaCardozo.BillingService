using System;

namespace OficinaCardozo.BillingService.Contracts.Events
{
    public class OsCreated
    {
        public Guid OsId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
