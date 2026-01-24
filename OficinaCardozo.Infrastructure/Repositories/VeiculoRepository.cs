using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Repositories;

public class VeiculoRepository : IVeiculoRepository
{
    private readonly OficinaDbContext _context;

    public VeiculoRepository(OficinaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Veiculo>> GetAllAsync()
    {
        return await _context.Veiculos
            .Include(v => v.Cliente)
            .AsNoTracking()
            .OrderBy(v => v.Placa)
            .ToListAsync();
    }

    public async Task<Veiculo?> GetByIdAsync(int id)
    {
        return await _context.Veiculos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Veiculo?> GetByIdWithClienteAsync(int id)
    {
        return await _context.Veiculos
            .Include(v => v.Cliente)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<IEnumerable<Veiculo>> GetByClienteIdAsync(int clienteId)
    {
        return await _context.Veiculos
            .Where(v => v.IdCliente == clienteId)
            .AsNoTracking()
            .OrderBy(v => v.Placa)
            .ToListAsync();
    }

    public async Task<Veiculo?> GetByPlacaAsync(string placa)
    {
        return await _context.Veiculos
            .Include(v => v.Cliente)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Placa == placa);
    }

    public async Task<Veiculo> CreateAsync(Veiculo veiculo)
    {
        _context.Veiculos.Add(veiculo);
        await _context.SaveChangesAsync();
        return veiculo;
    }

    public async Task<Veiculo> UpdateAsync(Veiculo veiculo)
    {
        _context.Veiculos.Update(veiculo);
        await _context.SaveChangesAsync();
        return veiculo;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var veiculo = await _context.Veiculos.FindAsync(id);
        if (veiculo == null)
            return false;

        _context.Veiculos.Remove(veiculo);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Veiculos.AnyAsync(v => v.Id == id);
    }

    public async Task<bool> ExistsByPlacaAsync(string placa, int? excludeId = null)
    {
        var query = _context.Veiculos.Where(v => v.Placa == placa);

        if (excludeId.HasValue)
            query = query.Where(v => v.Id != excludeId.Value);

        return await query.AnyAsync();
    }
}