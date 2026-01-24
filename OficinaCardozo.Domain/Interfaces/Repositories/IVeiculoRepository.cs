using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IVeiculoRepository
{
    Task<IEnumerable<Veiculo>> GetAllAsync();
    Task<Veiculo?> GetByIdAsync(int id);
    Task<Veiculo?> GetByIdWithClienteAsync(int id);
    Task<IEnumerable<Veiculo>> GetByClienteIdAsync(int clienteId);
    Task<Veiculo> CreateAsync(Veiculo veiculo);
    Task<Veiculo> UpdateAsync(Veiculo veiculo);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> ExistsByPlacaAsync(string placa, int? excludeId = null);

    Task<Veiculo?> GetByPlacaAsync(string placa);
}