using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OFICINACARDOZO.BILLINGSERVICE.Domain
{
    [Table("atualizacao_status_os")]
    public class AtualizacaoStatusOs
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("os_id")]
        public Guid OsId { get; set; }
        
        [Column("novo_status")]
        public string NovoStatus { get; set; } = string.Empty;
        
        [Column("event_type")]
        public string? EventType { get; set; }
        
        [Column("correlation_id")]
        public Guid? CorrelationId { get; set; }
        
        [Column("causation_id")]
        public Guid? CausationId { get; set; }
        
        [Column("atualizado_em")]
        public DateTime AtualizadoEm { get; set; }
    }
}