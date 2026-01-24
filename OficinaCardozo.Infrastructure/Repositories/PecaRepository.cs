using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Repositories;

public class PecaRepository : IPecaRepository
{
    private readonly OficinaDbContext _context;

    public PecaRepository(OficinaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Peca>> GetAllAsync()
    {
        return await _context.Pecas
            .AsNoTracking()
            .OrderBy(p => p.NomePeca)
            .ToListAsync();
    }

    public async Task<Peca?> GetByIdAsync(int id)
    {
        return await _context.Pecas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Peca>> GetEstoqueBaixoAsync()
    {
        return await _context.Pecas
            .Where(p => p.QuantidadeEstoque <= p.QuantidadeMinima)
            .AsNoTracking()
            .OrderBy(p => p.NomePeca)
            .ToListAsync();
    }

    public async Task<Peca> CreateAsync(Peca peca)
    {
        _context.Pecas.Add(peca);
        await _context.SaveChangesAsync();
        return peca;
    }

    public async Task<Peca> UpdateAsync(Peca peca)
    {
        _context.Pecas.Update(peca);
        await _context.SaveChangesAsync();
        return peca;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var peca = await _context.Pecas.FindAsync(id);
        if (peca == null)
            return false;

        _context.Pecas.Remove(peca);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Pecas.AnyAsync(p => p.Id == id);
    }

    public async Task<bool> ExistsByCodigoAsync(string codigoIdentificador, int? excludeId = null)
    {
        var query = _context.Pecas.Where(p => p.CodigoIdentificador == codigoIdentificador);

        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> MovimentarEstoqueAsync(int id, int quantidade, string tipo)
    {
        var peca = await _context.Pecas.FindAsync(id);
        if (peca == null)
            return false;

        if (tipo.ToUpper() == "ENTRADA")
        {
            peca.QuantidadeEstoque += quantidade;
        }
        else if (tipo.ToUpper() == "SAIDA")
        {
            if (peca.QuantidadeEstoque < quantidade)
                throw new InvalidOperationException("Quantidade em estoque insuficiente");

            peca.QuantidadeEstoque -= quantidade;
        }
        else
        {
            throw new ArgumentException("Tipo de movimenta��o inv�lido. Use ENTRADA ou SAIDA");
        }

        await _context.SaveChangesAsync();
        return true;
    }
}