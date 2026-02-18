using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class BudgetRejected
    {
        public Guid OsId { get; set; }
        public Guid BudgetId { get; set; }
        public BudgetStatus Status { get; set; }
        public string Reason { get; set; }
    }
}
