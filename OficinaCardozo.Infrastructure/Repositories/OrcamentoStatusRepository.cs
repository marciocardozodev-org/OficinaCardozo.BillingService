using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.App.OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Repositories;

public class OrcamentoStatusRepository : IOrcamentoStatusRepository
{
    private readonly OficinaDbContext _context;

    public OrcamentoStatusRepository(OficinaDbContext context)
    {
        _context = context;
    }

    public async Task<OrcamentoStatus?> GetByDescricaoAsync(string descricao)
    {
        return await _context.OrcamentoStatus
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Descricao == descricao);
    }

    public async Task<IEnumerable<OrcamentoStatus>> GetAllAsync()
    {
        return await _context.OrcamentoStatus
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync();
    }
}