using System.Text.Json.Serialization;

namespace OFICINACARDOZO.BILLINGSERVICE.Domain
{
    public class BudgetApproved
    {
        [JsonPropertyName("orcamentoId")]
        public long OrcamentoId { get; set; }

        [JsonPropertyName("osId")]
        public Guid OsId { get; set; }

        [JsonPropertyName("valor")]
        public decimal Valor { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Aprovado";

        [JsonPropertyName("approvedAt")]
        public DateTime ApprovedAt { get; set; }

        [JsonPropertyName("correlationId")]
        public Guid CorrelationId { get; set; }

        [JsonPropertyName("causationId")]
        public Guid CausationId { get; set; }
    }
}
