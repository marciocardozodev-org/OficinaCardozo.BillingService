using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IOrdemServicoRepository
{
    Task<OrdemServico> CreateAsync(OrdemServico ordemServico);
    Task<OrdemServico?> GetByIdAsync(int id);
    Task<OrdemServico?> GetByIdWithDetailsAsync(int id);
    Task<IEnumerable<OrdemServico>> GetAllAsync();
    Task<IEnumerable<OrdemServico>> GetAllWithDetailsAsync();
    Task<OrdemServico> UpdateAsync(OrdemServico ordemServico);
    Task<bool> DeleteAsync(int id);
    Task<int> DeleteAllAsync(); 
    Task<OrdemServicoServico> AddOrdemServicoServicoAsync(OrdemServicoServico ordemServicoServico);
    Task<OrdemServicoPeca> AddOrdemServicoPecaAsync(OrdemServicoPeca ordemServicoPeca);
}