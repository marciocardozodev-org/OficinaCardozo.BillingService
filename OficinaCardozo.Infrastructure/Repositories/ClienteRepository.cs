using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Repositories;

public class ClienteRepository : IClienteRepository
{
    private readonly OficinaDbContext _context;

    public ClienteRepository(OficinaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region M�todos em ingl�s (existentes)

    public async Task<IEnumerable<Cliente>> GetAllAsync()
    {
        return await _context.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    public async Task<Cliente?> GetByIdAsync(int id)
    {
        return await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cliente?> GetByIdWithVeiculosAsync(int id)
    {
        return await _context.Clientes
            .Include(c => c.Veiculos)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cliente> CreateAsync(Cliente cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task<Cliente> UpdateAsync(Cliente cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        _context.Clientes.Update(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
            return false;

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Clientes
            .AsNoTracking()
            .AnyAsync(c => c.Id == id);
    }

    public async Task<bool> ExistsByCpfCnpjAsync(string cpfCnpj, int? excludeId = null)
    {
        var query = _context.Clientes.AsNoTracking();

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync(c => c.CpfCnpj == cpfCnpj);
    }

    public async Task<Cliente?> GetByCpfCnpjAsync(string cpfCnpj)
    {
        return await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CpfCnpj == cpfCnpj);
    }

    #endregion

    #region M�todos em portugu�s (nova nomenclatura)

    public async Task<IEnumerable<Cliente>> ObterTodosAsync()
        => await GetAllAsync();

    public async Task<Cliente?> ObterPorIdAsync(int id)
        => await GetByIdAsync(id);

    public async Task<Cliente?> ObterPorIdComVeiculosAsync(int id)
        => await GetByIdWithVeiculosAsync(id);

    public async Task<Cliente> CriarAsync(Cliente cliente)
        => await CreateAsync(cliente);

    public async Task<Cliente> AtualizarAsync(Cliente cliente)
        => await UpdateAsync(cliente);

    public async Task<bool> RemoverAsync(int id)
        => await DeleteAsync(id);

    public async Task<bool> ExisteAsync(int id)
        => await ExistsAsync(id);

    public async Task<bool> ExistePorCpfCnpjAsync(string cpfCnpj, int? idExcluir = null)
        => await ExistsByCpfCnpjAsync(cpfCnpj, idExcluir);

    public async Task<Cliente?> ObterPorCpfCnpjAsync(string cpfCnpj)
        => await GetByCpfCnpjAsync(cpfCnpj);

    #endregion
}