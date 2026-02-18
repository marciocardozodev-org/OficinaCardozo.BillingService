using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class OsCompensationRequested
    {
        public Guid OsId { get; set; }
        public string Reason { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}
