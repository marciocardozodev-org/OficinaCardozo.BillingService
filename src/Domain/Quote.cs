using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OFICINACARDOZO.BILLINGSERVICE.Domain
{
    public enum StatusOrcamento { Pendente, Enviado, Aprovado, Rejeitado }
    [Table("orcamento")]
    public class Orcamento
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }
        
        [Column("os_id")]
        public Guid OsId { get; set; }
        
        [Column("valor")]
        public decimal Valor { get; set; }
        
        [Column("email_cliente")]
        public string EmailCliente { get; set; } = string.Empty;
        
        [Column("status")]
        public StatusOrcamento Status { get; set; }
        
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