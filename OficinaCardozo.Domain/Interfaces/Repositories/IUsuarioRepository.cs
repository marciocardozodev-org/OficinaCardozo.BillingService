using OficinaCardozo.Domain.Entities;

namespace OficinaCardozo.Domain.Interfaces.Repositories;

public interface IUsuarioRepository
{
    Task<IEnumerable<Usuario>> GetAllAsync();

    Task<Usuario?> GetByIdAsync(int id);

    Task<Usuario?> GetByNomeUsuarioAsync(string nomeUsuario);

    Task<Usuario?> GetByEmailAsync(string email);

    Task<Usuario> CreateAsync(Usuario usuario);

    Task<Usuario> UpdateAsync(Usuario usuario);

    Task<bool> DeleteAsync(int id);

    Task<bool> ExistsAsync(int id);

    Task<bool> ExistsByNomeUsuarioAsync(string nomeUsuario, int? excludeId = null);

    Task<bool> ExistsByEmailAsync(string email, int? excludeId = null);
}