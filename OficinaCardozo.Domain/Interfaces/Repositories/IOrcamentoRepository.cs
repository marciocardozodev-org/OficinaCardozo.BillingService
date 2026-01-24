using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IOrcamentoRepository
{
    Task<Orcamento> CreateAsync(Orcamento orcamento);
    Task<Orcamento?> GetByIdAsync(int id);
    Task<Orcamento?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<Orcamento>> GetAllAsync();
    Task<IEnumerable<Orcamento>> GetAllWithDetailsAsync();
    Task<IEnumerable<Orcamento>> GetByOrdemServicoIdAsync(int idOrdemServico);
    Task<Orcamento> UpdateAsync(Orcamento orcamento);
    Task<bool> DeleteAsync(int id);
    Task<int> DeleteAllAsync(); 
}