using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OficinaCardozo.Application.DTOs;
using OficinaCardozo.Application.Settings;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.Domain.Interfaces.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OficinaCardozo.Application.Services;

public class AutenticacaoService : IAutenticacaoService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly ConfiguracoesJwt _configuracoesJwt;

    public AutenticacaoService(IUsuarioRepository usuarioRepository, IClienteRepository clienteRepository, IOptions<ConfiguracoesJwt> configuracoesJwt)
    {
        _usuarioRepository = usuarioRepository;
        _clienteRepository = clienteRepository;
        _configuracoesJwt = configuracoesJwt.Value;
    }

    public async Task<TokenRespostaDto> FazerLoginAsync(LoginDto loginDto)
    {
        var usuario = await _usuarioRepository.GetByNomeUsuarioAsync(loginDto.NomeUsuario);

        if (usuario == null || !VerificarSenha(loginDto.Senha, usuario.HashSenha, usuario.Complexidade))
        {
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        var usuarioDto = MapToDto(usuario);

        var token = GerarTokenJwt(usuarioDto);

        return new TokenRespostaDto
        {
            Token = token,
            ExpiraEm = DateTime.UtcNow.AddMinutes(_configuracoesJwt.ExpiracaoEmMinutos),
            NomeUsuario = usuario.NomeUsuario
        };
    }

    public async Task<TokenRespostaDto> FazerLoginPorCpfAsync(LoginCpfDto loginCpfDto)
    {
        var cliente = await _clienteRepository.GetByCpfCnpjAsync(loginCpfDto.CpfCnpj);

        if (cliente == null)
        {
            throw new UnauthorizedAccessException("CPF/CNPJ não encontrado");
        }

        var usuario = await _usuarioRepository.GetByNomeUsuarioAsync(loginCpfDto.CpfCnpj);

        if (usuario == null || !VerificarSenha(loginCpfDto.Senha, usuario.HashSenha, usuario.Complexidade))
        {
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        var token = GerarTokenJwtCliente(cliente);

        return new TokenRespostaDto
        {
            Token = token,
            ExpiraEm = DateTime.UtcNow.AddMinutes(_configuracoesJwt.ExpiracaoEmMinutos),
            NomeUsuario = cliente.Nome
        };
    }

    public async Task<UsuarioDto> CriarUsuarioAsync(CriarUsuarioDto criarUsuarioDto)
    {
     
        if (await _usuarioRepository.ExistsByNomeUsuarioAsync(criarUsuarioDto.NomeUsuario))
            throw new InvalidOperationException("Nome de usuário já está em uso");

        if (await _usuarioRepository.ExistsByEmailAsync(criarUsuarioDto.Email))
            throw new InvalidOperationException("Email já está em uso");
      
        var complexidade = GerarComplexidade();

        var hashSenha = GerarHashSenha(criarUsuarioDto.Senha, complexidade);

        var usuario = new Usuario
        {
            NomeUsuario = criarUsuarioDto.NomeUsuario,
            Email = criarUsuarioDto.Email,
            HashSenha = hashSenha,           
            Complexidade = complexidade,     
            DataCriacao = DateTime.UtcNow    
        };

      
        var usuarioCriado = await _usuarioRepository.CreateAsync(usuario);

      
        return MapToDto(usuarioCriado);
    }


    public async Task<UsuarioDto> AtualizarUsuarioAsync(int id, AtualizarUsuarioDto atualizarUsuarioDto)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id);
        if (usuario == null)
            throw new KeyNotFoundException("Usuário não encontrado");

        if (await _usuarioRepository.ExistsByNomeUsuarioAsync(atualizarUsuarioDto.NomeUsuario, id))
            throw new InvalidOperationException("Nome de usuário já está em uso por outro usuário");

        if (await _usuarioRepository.ExistsByEmailAsync(atualizarUsuarioDto.Email, id))
            throw new InvalidOperationException("Email já está em uso por outro usuário");

        usuario.NomeUsuario = atualizarUsuarioDto.NomeUsuario;
        usuario.Email = atualizarUsuarioDto.Email;

        var usuarioAtualizado = await _usuarioRepository.UpdateAsync(usuario);

        return MapToDto(usuarioAtualizado);
    }

    public async Task<bool> ExcluirUsuarioAsync(int id)
    {
        if (!await _usuarioRepository.ExistsAsync(id))
            throw new KeyNotFoundException("Usuário não encontrado");

        // PASSO 2: Excluir via repository
        return await _usuarioRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<UsuarioDto>> ObterTodosUsuariosAsync()
    {
        var usuarios = await _usuarioRepository.GetAllAsync();
        return usuarios.Select(MapToDto);
    }

    public async Task<UsuarioDto?> ObterUsuarioPorIdAsync(int id)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id);
        return usuario != null ? MapToDto(usuario) : null;
    }

    public async Task<bool> ValidarTokenAsync(string token)
    {
        try
        {
            var manipuladorToken = new JwtSecurityTokenHandler();
            var chave = Encoding.ASCII.GetBytes(_configuracoesJwt.ChaveSecreta);

            var parametrosValidacao = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(chave),
                ValidateIssuer = false,            
                ValidateAudience = false,          
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            manipuladorToken.ValidateToken(token, parametrosValidacao, out SecurityToken tokenValidado);
            return await Task.FromResult(true);
        }
        catch
        {
            return await Task.FromResult(false);
        }
    }

    public string GerarTokenJwt(UsuarioDto usuario)
    {
        var manipuladorToken = new JwtSecurityTokenHandler();

        var chave = Encoding.ASCII.GetBytes(_configuracoesJwt.ChaveSecreta);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),  
            new(ClaimTypes.Name, usuario.NomeUsuario),              
            new(ClaimTypes.Email, usuario.Email),                  

            new("idUsuario", usuario.Id.ToString()),     
            new("nomeUsuario", usuario.NomeUsuario)      
        };

     
        var descritorToken = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),       
            Expires = DateTime.UtcNow.AddMinutes(_configuracoesJwt.ExpiracaoEmMinutos), 
            SigningCredentials = new SigningCredentials( 
                new SymmetricSecurityKey(chave),         
                SecurityAlgorithms.HmacSha256Signature   
            )
        };

        var token = manipuladorToken.CreateToken(descritorToken);
        return manipuladorToken.WriteToken(token);
    }

    private string GerarTokenJwtCliente(Cliente cliente)
    {
        var manipuladorToken = new JwtSecurityTokenHandler();

        var chave = Encoding.ASCII.GetBytes(_configuracoesJwt.ChaveSecreta);


        var claims = new List<Claim>
        {
            new("clienteId", cliente.Id.ToString()),
            new("cpfCnpj", cliente.CpfCnpj),
            new(ClaimTypes.Name, cliente.Nome)
        };
        // Se o cpfCnpj for um CPF válido (11 dígitos), adiciona claim "cpf" para compatibilidade com policy
        if (!string.IsNullOrEmpty(cliente.CpfCnpj) && cliente.CpfCnpj.Length == 11)
        {
            claims.Add(new Claim("cpf", cliente.CpfCnpj));
        }

        var descritorToken = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_configuracoesJwt.ExpiracaoEmMinutos),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(chave),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = manipuladorToken.CreateToken(descritorToken);
        return manipuladorToken.WriteToken(token);
    }

   
    private static string GerarComplexidade()
    {
        var complexidade = new byte[32];                    
        using var gerador = RandomNumberGenerator.Create(); 
        gerador.GetBytes(complexidade);                     
        return Convert.ToBase64String(complexidade);        
    }
    
    private static string GerarHashSenha(string senha, string complexidade)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            senha,                                     
            Convert.FromBase64String(complexidade),    
            10000,                                     
            HashAlgorithmName.SHA256                   
        );
        var hash = pbkdf2.GetBytes(32);                
        return Convert.ToBase64String(hash);           
    }

   
    private static bool VerificarSenha(string senha, string hash, string complexidade)
    {
        var hashSenha = GerarHashSenha(senha, complexidade); 
        return hashSenha == hash;                            
    }

    private static UsuarioDto MapToDto(Usuario usuario)
    {
        return new UsuarioDto
        {
            Id = usuario.Id,
            NomeUsuario = usuario.NomeUsuario,
            Email = usuario.Email,
            DataCriacao = usuario.DataCriacao
           
        };
    }
}