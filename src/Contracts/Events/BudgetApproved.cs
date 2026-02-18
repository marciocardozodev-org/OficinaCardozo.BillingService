using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class BudgetApproved
    {
        public Guid OsId { get; set; }
        public Guid BudgetId { get; set; }
        public BudgetStatus Status { get; set; }
    }
}
