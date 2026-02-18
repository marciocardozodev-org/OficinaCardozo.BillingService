using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OFICINACARDOZO.BILLINGSERVICE.Domain
{
    public enum StatusPagamento { Pendente, Confirmado, Falhou }
    [Table("pagamento")]
    public class Pagamento
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("os_id")]
        public Guid OsId { get; set; }
        
        [Column("orcamento_id")]
        public long OrcamentoId { get; set; }
        
        [Column("valor")]
        public decimal Valor { get; set; }
        
        [Column("metodo")]
        public string Metodo { get; set; } = string.Empty;
        
        [Column("status")]
        public StatusPagamento Status { get; set; }
        
        [Column("provider_payment_id")]
        public string? ProviderPaymentId { get; set; }
        
        [Column("correlation_id")]
        public Guid CorrelationId { get; set; }
        
        [Column("causation_id")]
        public Guid CausationId { get; set; }
        
        [Column("criado_em")]
        public DateTime CriadoEm { get; set; }
        
        [Column("atualizado_em")]
        public DateTime AtualizadoEm { get; set; }
    }
}