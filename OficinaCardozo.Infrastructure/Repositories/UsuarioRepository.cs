using Microsoft.EntityFrameworkCore;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.App.OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Infrastructure.Data;

namespace OficinaCardozo.Infrastructure.Repositories;


public class UsuarioRepository : IUsuarioRepository
{
    private readonly OficinaDbContext _context;

    public UsuarioRepository(OficinaDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Usuario>> GetAllAsync()
    {
        return await _context.Usuarios
            .OrderBy(u => u.NomeUsuario)  
            .ToListAsync();               
    }

    public async Task<Usuario?> GetByIdAsync(int id)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<Usuario?> GetByNomeUsuarioAsync(string nomeUsuario)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.NomeUsuario == nomeUsuario);
    }

    public async Task<Usuario?> GetByEmailAsync(string email)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<Usuario> CreateAsync(Usuario usuario)
    {
        await _context.Usuarios.AddAsync(usuario);   
        await _context.SaveChangesAsync();           
        return usuario;                              
    }

    public async Task<Usuario> UpdateAsync(Usuario usuario)
    {
        _context.Usuarios.Update(usuario);          
        await _context.SaveChangesAsync();          
        return usuario;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var usuario = await GetByIdAsync(id);
        if (usuario == null) return false;          

        _context.Usuarios.Remove(usuario);          
        await _context.SaveChangesAsync();          
        return true;                                
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Usuarios
            .AnyAsync(u => u.Id == id);
    }

   
    public async Task<bool> ExistsByNomeUsuarioAsync(string nomeUsuario, int? excludeId = null)
    {
        return await _context.Usuarios
            .AnyAsync(u => u.NomeUsuario == nomeUsuario &&
                          (excludeId == null || u.Id != excludeId));
    }

    public async Task<bool> ExistsByEmailAsync(string email, int? excludeId = null)
    {
        return await _context.Usuarios
            .AnyAsync(u => u.Email == email &&
                          (excludeId == null || u.Id != excludeId));
    }
}