using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IOrcamentoStatusRepository
{
    Task<OrcamentoStatus?> GetByDescricaoAsync(string descricao);
    Task<IEnumerable<OrcamentoStatus>> GetAllAsync();
}