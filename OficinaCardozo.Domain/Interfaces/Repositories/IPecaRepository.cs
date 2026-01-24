using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IPecaRepository
{
    Task<IEnumerable<Peca>> GetAllAsync();
    Task<Peca?> GetByIdAsync(int id);
    Task<IEnumerable<Peca>> GetEstoqueBaixoAsync();
    Task<Peca> CreateAsync(Peca peca);
    Task<Peca> UpdateAsync(Peca peca);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> ExistsByCodigoAsync(string codigoIdentificador, int? excludeId = null);
    Task<bool> MovimentarEstoqueAsync(int id, int quantidade, string tipo);
}