using OficinaCardozo.Application.DTOs;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.App.OficinaCardozo.Domain.Interfaces.Repositories;

namespace OficinaCardozo.Application.Services;

public interface IPecaService
{
    Task<IEnumerable<PecaDto>> GetAllAsync();
    Task<PecaDto?> GetByIdAsync(int id);
    Task<IEnumerable<PecaDto>> GetEstoqueBaixoAsync();
    Task<PecaDto> CreateAsync(CreatePecaDto createDto);
    Task<PecaDto> UpdateAsync(int id, UpdatePecaDto updateDto);
    Task<bool> DeleteAsync(int id);
    Task<bool> MovimentarEstoqueAsync(int id, MovimentacaoEstoqueDto movimentacaoDto);
}

public class PecaService : IPecaService
{
    private readonly IPecaRepository _pecaRepository;

    public PecaService(IPecaRepository pecaRepository)
    {
        _pecaRepository = pecaRepository;
    }

    public async Task<IEnumerable<PecaDto>> GetAllAsync()
    {
        var pecas = await _pecaRepository.GetAllAsync();
        return pecas.Select(MapToDto);
    }

    public async Task<PecaDto?> GetByIdAsync(int id)
    {
        var peca = await _pecaRepository.GetByIdAsync(id);
        return peca != null ? MapToDto(peca) : null;
    }

    public async Task<IEnumerable<PecaDto>> GetEstoqueBaixoAsync()
    {
        var pecas = await _pecaRepository.GetEstoqueBaixoAsync();
        return pecas.Select(MapToDto);
    }

    public async Task<PecaDto> CreateAsync(CreatePecaDto createDto)
    {
        if (await _pecaRepository.ExistsByCodigoAsync(createDto.CodigoIdentificador))
            throw new InvalidOperationException("C�digo identificador j� cadastrado no sistema");

        var peca = new Peca
        {
            NomePeca = createDto.NomePeca,
            CodigoIdentificador = createDto.CodigoIdentificador,
            Preco = createDto.Preco,
            QuantidadeEstoque = createDto.QuantidadeEstoque,
            QuantidadeMinima = createDto.QuantidadeMinima,
            UnidadeMedida = createDto.UnidadeMedida,
            LocalizacaoEstoque = createDto.LocalizacaoEstoque,
            Observacoes = createDto.Observacoes
        };

        var createdPeca = await _pecaRepository.CreateAsync(peca);
        return MapToDto(createdPeca);
    }

    public async Task<PecaDto> UpdateAsync(int id, UpdatePecaDto updateDto)
    {
        var peca = await _pecaRepository.GetByIdAsync(id);
        if (peca == null)
            throw new KeyNotFoundException("Pe�a n�o encontrada");

        if (!string.IsNullOrWhiteSpace(updateDto.CodigoIdentificador) &&
            await _pecaRepository.ExistsByCodigoAsync(updateDto.CodigoIdentificador, id))
            throw new InvalidOperationException("C�digo identificador j� cadastrado para outra pe�a");

        if (!string.IsNullOrWhiteSpace(updateDto.NomePeca))
            peca.NomePeca = updateDto.NomePeca;

        if (!string.IsNullOrWhiteSpace(updateDto.CodigoIdentificador))
            peca.CodigoIdentificador = updateDto.CodigoIdentificador;

        if (updateDto.Preco.HasValue)
            peca.Preco = updateDto.Preco.Value;

        if (updateDto.QuantidadeEstoque.HasValue)
            peca.QuantidadeEstoque = updateDto.QuantidadeEstoque.Value;

        if (updateDto.QuantidadeMinima.HasValue)
            peca.QuantidadeMinima = updateDto.QuantidadeMinima.Value;

        if (!string.IsNullOrWhiteSpace(updateDto.UnidadeMedida))
            peca.UnidadeMedida = updateDto.UnidadeMedida;

        if (updateDto.LocalizacaoEstoque != null)
            peca.LocalizacaoEstoque = updateDto.LocalizacaoEstoque;

        if (updateDto.Observacoes != null)
            peca.Observacoes = updateDto.Observacoes;

        var updatedPeca = await _pecaRepository.UpdateAsync(peca);
        return MapToDto(updatedPeca);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (!await _pecaRepository.ExistsAsync(id))
            throw new KeyNotFoundException("Pe�a n�o encontrada");

        return await _pecaRepository.DeleteAsync(id);
    }

    public async Task<bool> MovimentarEstoqueAsync(int id, MovimentacaoEstoqueDto movimentacaoDto)
    {
        if (!await _pecaRepository.ExistsAsync(id))
            throw new KeyNotFoundException("Pe�a n�o encontrada");

        return await _pecaRepository.MovimentarEstoqueAsync(id, movimentacaoDto.Quantidade, movimentacaoDto.Tipo);
    }

    private static PecaDto MapToDto(Peca peca)
    {
        return new PecaDto
        {
            Id = peca.Id,
            NomePeca = peca.NomePeca,
            CodigoIdentificador = peca.CodigoIdentificador,
            Preco = peca.Preco,
            QuantidadeEstoque = peca.QuantidadeEstoque,
            QuantidadeMinima = peca.QuantidadeMinima,
            UnidadeMedida = peca.UnidadeMedida,
            LocalizacaoEstoque = peca.LocalizacaoEstoque,
            Observacoes = peca.Observacoes
        };
    }
}