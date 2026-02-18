using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class BudgetGenerated
    {
        public Guid OsId { get; set; }
        public Guid BudgetId { get; set; }
        public decimal Amount { get; set; }
        public BudgetStatus Status { get; set; }
    }
}
