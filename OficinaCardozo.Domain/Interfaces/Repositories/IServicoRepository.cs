using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IServicoRepository
{
    Task<IEnumerable<Servico>> GetAllAsync();
    Task<Servico?> GetByIdAsync(int id);
    Task<Servico> CreateAsync(Servico servico);
    Task<Servico> UpdateAsync(Servico servico);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> ExistsByNomeAsync(string nomeServico, int? excludeId = null);
}