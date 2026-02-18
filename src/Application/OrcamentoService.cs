using OFICINACARDOZO.BILLINGSERVICE.Domain;
using Microsoft.EntityFrameworkCore;

namespace OFICINACARDOZO.BILLINGSERVICE.Application
{
    public class OrcamentoService
    {
        private readonly BillingDbContext _db;
        public OrcamentoService(BillingDbContext db)
        {
            _db = db;
        }

        public async Task<Orcamento> GerarEEnviarOrcamentoAsync(Guid osId, decimal valor, string emailCliente, Guid correlationId, Guid causationId)
        {
            var orcamento = new Orcamento
            {
                OsId = osId,
                Valor = valor,
                EmailCliente = emailCliente,
                Status = StatusOrcamento.Enviado,
                CorrelationId = correlationId,
                CausationId = causationId,
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            };
            _db.Orcamentos.Add(orcamento);
            await _db.SaveChangesAsync();
            return orcamento;
        }

        public async Task<Orcamento?> GetBudgetByOsIdAsync(Guid osId)
        {
            return await _db.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        }
    }
}