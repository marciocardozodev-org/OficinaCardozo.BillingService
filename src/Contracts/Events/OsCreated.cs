using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class OsCreated
    {
        public Guid OsId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
