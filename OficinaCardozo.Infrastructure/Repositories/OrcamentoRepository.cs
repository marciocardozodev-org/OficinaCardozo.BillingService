using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Repositories;

public class OrcamentoRepository : IOrcamentoRepository
{
    private readonly OficinaDbContext _context;

    public OrcamentoRepository(OficinaDbContext context)
    {
        _context = context;
    }

    public async Task<Orcamento> CreateAsync(Orcamento orcamento)
    {
        _context.Orcamentos.Add(orcamento);
        await _context.SaveChangesAsync();
        return orcamento;
    }

    public async Task<Orcamento?> GetByIdAsync(int id)
    {
        return await _context.Orcamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Orcamento?> GetByIdWithDetailsAsync(int id)
    {
        return await _context.Orcamentos
            .Include(o => o.Status)
            .Include(o => o.OrdemServico)
                .ThenInclude(os => os.Veiculo)
                    .ThenInclude(v => v.Cliente)
            .Include(o => o.OrdemServico)
                .ThenInclude(os => os.OrdemServicoServicos)
                    .ThenInclude(oss => oss.Servico)
            .Include(o => o.OrdemServico)
                .ThenInclude(os => os.OrdemServicoPecas)
                    .ThenInclude(osp => osp.Peca)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<IEnumerable<Orcamento>> GetAllAsync()
    {
        return await _context.Orcamentos
            .AsNoTracking()
            .OrderByDescending(o => o.DataOrcamento)
            .ToListAsync();
    }

    public async Task<IEnumerable<Orcamento>> GetAllWithDetailsAsync()
    {
        return await _context.Orcamentos
            .Include(o => o.Status)
            .Include(o => o.OrdemServico)
                .ThenInclude(os => os.Veiculo)
                    .ThenInclude(v => v.Cliente)
            .Include(o => o.OrdemServico)
                .ThenInclude(os => os.OrdemServicoServicos)
                    .ThenInclude(oss => oss.Servico)
            .Include(o => o.OrdemServico)
                .ThenInclude(os => os.OrdemServicoPecas)
                    .ThenInclude(osp => osp.Peca)
            .AsNoTracking()
            .OrderByDescending(o => o.DataOrcamento)
            .ToListAsync();
    }

    public async Task<IEnumerable<Orcamento>> GetByOrdemServicoIdAsync(int idOrdemServico)
    {
        return await _context.Orcamentos
            .Include(o => o.Status)
            .Where(o => o.IdOrdemServico == idOrdemServico)
            .AsNoTracking()
            .OrderByDescending(o => o.DataOrcamento)
            .ToListAsync();
    }

    public async Task<Orcamento> UpdateAsync(Orcamento orcamento)
    {
        var existingOrcamento = await _context.Orcamentos
            .FirstOrDefaultAsync(o => o.Id == orcamento.Id);

        if (existingOrcamento == null)
            throw new KeyNotFoundException($"Or�amento com ID {orcamento.Id} n�o encontrado");

        existingOrcamento.IdStatus = orcamento.IdStatus;
        existingOrcamento.DataOrcamento = orcamento.DataOrcamento;

        await _context.SaveChangesAsync();
        return existingOrcamento;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var orcamento = await _context.Orcamentos.FindAsync(id);
        if (orcamento == null)
            return false;

        _context.Orcamentos.Remove(orcamento);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteAllAsync()
    {
        var orcamentos = await _context.Orcamentos.ToListAsync();
        var count = orcamentos.Count;

        if (count > 0)
        {
            _context.Orcamentos.RemoveRange(orcamentos);
            await _context.SaveChangesAsync();
        }

        return count;
    }
}