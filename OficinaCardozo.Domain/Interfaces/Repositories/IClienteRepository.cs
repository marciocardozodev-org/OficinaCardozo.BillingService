using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IClienteRepository
{
    Task<IEnumerable<Cliente>> GetAllAsync();
    Task<Cliente?> GetByIdAsync(int id);
    Task<Cliente?> GetByIdWithVeiculosAsync(int id);
    Task<Cliente> CreateAsync(Cliente cliente);
    Task<Cliente> UpdateAsync(Cliente cliente);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<bool> ExistsByCpfCnpjAsync(string cpfCnpj, int? excludeId = null);
    Task<Cliente?> GetByCpfCnpjAsync(string cpfCnpj);

    Task<IEnumerable<Cliente>> ObterTodosAsync();
    Task<Cliente?> ObterPorIdAsync(int id);
    Task<Cliente?> ObterPorIdComVeiculosAsync(int id);
    Task<Cliente> CriarAsync(Cliente cliente);
    Task<Cliente> AtualizarAsync(Cliente cliente);
    Task<bool> RemoverAsync(int id);
    Task<bool> ExisteAsync(int id);
    Task<bool> ExistePorCpfCnpjAsync(string cpfCnpj, int? idExcluir = null);
    Task<Cliente?> ObterPorCpfCnpjAsync(string cpfCnpj);
}