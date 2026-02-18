using OFICINACARDOZO.BILLINGSERVICE.Domain;
using Microsoft.EntityFrameworkCore;

namespace OFICINACARDOZO.BILLINGSERVICE.Application
{
    public class AtualizacaoStatusOsService
    {
        private readonly BillingDbContext _context;
        
        public AtualizacaoStatusOsService(BillingDbContext context)
        {
            _context = context;
        }

        public AtualizacaoStatusOs AtualizarStatus(
            Guid osId, 
            string novoStatus,
            string? eventType = null,
            Guid? correlationId = null,
            Guid? causationId = null)
        {
            var atualizacao = new AtualizacaoStatusOs
            {
                OsId = osId,
                NovoStatus = novoStatus,
                EventType = eventType,
                CorrelationId = correlationId,
                CausationId = causationId,
                AtualizadoEm = DateTime.UtcNow
            };
            _context.AtualizacoesStatusOs.Add(atualizacao);
            _context.SaveChanges();
            return atualizacao;
        }
        
        public IEnumerable<AtualizacaoStatusOs> ListarPorOrdem(Guid osId) => 
            _context.AtualizacoesStatusOs
                .Where(a => a.OsId == osId)
                .ToList();
    }
}