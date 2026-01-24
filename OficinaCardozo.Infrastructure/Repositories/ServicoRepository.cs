using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Repositories;

public class ServicoRepository : IServicoRepository
{
    private readonly OficinaDbContext _context;

    public ServicoRepository(OficinaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Servico>> GetAllAsync()
    {
        return await _context.Servicos
            .AsNoTracking()
            .OrderBy(s => s.NomeServico)
            .ToListAsync();
    }

    public async Task<Servico?> GetByIdAsync(int id)
    {
        return await _context.Servicos
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Servico> CreateAsync(Servico servico)
    {
        _context.Servicos.Add(servico);
        await _context.SaveChangesAsync();
        return servico;
    }

    public async Task<Servico> UpdateAsync(Servico servico)
    {
        _context.Servicos.Update(servico);
        await _context.SaveChangesAsync();
        return servico;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var servico = await _context.Servicos.FindAsync(id);
        if (servico == null)
            return false;

        _context.Servicos.Remove(servico);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Servicos.AnyAsync(s => s.Id == id);
    }

    public async Task<bool> ExistsByNomeAsync(string nomeServico, int? excludeId = null)
    {
        var query = _context.Servicos.Where(s => s.NomeServico == nomeServico);

        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return await query.AnyAsync();
    }
}