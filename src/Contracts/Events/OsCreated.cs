using System;

namespace OFICINACARDOZO.BILLINGSERVICE.Contracts.Events
{
    public class OsCreated
    {
        public Guid OsId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Valor da OS para cobrança. Se nulo ou <=0, usa fallback (100.00).
        /// Campo opcional para compatibilidade retroativa.
        /// </summary>
        public decimal? Valor { get; set; }
    }
}
