using OficinaCardozo.Application.DTOs;
using OficinaCardozo.Application.Interfaces;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.App.OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Domain.ValueObjects;
using OficinaCardozo.Domain.Exceptions;

namespace OficinaCardozo.Application.Services;

public class VeiculoService : IVeiculoService
{
    private readonly IVeiculoRepository _veiculoRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IVeiculoMapper _veiculoMapper;

    public VeiculoService(
        IVeiculoRepository veiculoRepository,
        IClienteRepository clienteRepository,
        IVeiculoMapper veiculoMapper
        )
    {
        _veiculoRepository = veiculoRepository ?? throw new ArgumentNullException(nameof(veiculoRepository));
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _veiculoMapper = veiculoMapper ?? throw new ArgumentNullException(nameof(veiculoMapper));

    }
    #region M�todos em portugu�s (Clean Architecture)

    public async Task<IEnumerable<VeiculoDto>> ObterTodosVeiculosAsync()
    {
        var veiculos = await _veiculoRepository.GetAllAsync();
        return _veiculoMapper.MapearParaListaDto(veiculos);
    }

    public async Task<VeiculoDto?> ObterVeiculoPorIdAsync(int id)
    {
        ValidarId(id);

        var veiculo = await _veiculoRepository.GetByIdWithClienteAsync(id);
        return veiculo != null ? _veiculoMapper.MapearParaDto(veiculo) : null;
    }

    public async Task<IEnumerable<VeiculoDto>> ObterVeiculosPorClienteAsync(int clienteId)
    {
        ValidarId(clienteId);

        var veiculos = await _veiculoRepository.GetByClienteIdAsync(clienteId);
        return _veiculoMapper.MapearParaListaDto(veiculos);
    }

    public async Task<VeiculoDto?> ObterVeiculoPorPlacaAsync(string placa)
    {
        if (string.IsNullOrWhiteSpace(placa))
            throw new ArgumentException("Placa n�o pode estar vazia", nameof(placa));

        var placaObj = new Placa(placa);
        var veiculo = await _veiculoRepository.GetByPlacaAsync(placaObj.Valor);

        return veiculo != null ? _veiculoMapper.MapearParaDto(veiculo) : null;
    }

    public async Task<VeiculoDto> CriarVeiculoAsync(CreateVeiculoDto createDto)
    {
        ValidarDtoCreate(createDto);

        await ValidarClienteExisteAsync(createDto.IdCliente);

        var placa = new Placa(createDto.Placa);
        await ValidarPlacaUnicaAsync(placa);

        var novoVeiculo = _veiculoMapper.MapearParaEntidade(createDto);
        novoVeiculo.Placa = placa.Valor;

        var veiculoCriado = await _veiculoRepository.CreateAsync(novoVeiculo);
        return _veiculoMapper.MapearParaDto(veiculoCriado);
    }

    public async Task<VeiculoDto> AtualizarVeiculoAsync(int id, UpdateVeiculoDto updateDto)
    {
        ValidarId(id);
        ValidarDtoUpdate(updateDto);

        var veiculo = await ObterVeiculoExistenteAsync(id);

        if (updateDto.IdCliente.HasValue)
        {
            await ValidarClienteExisteAsync(updateDto.IdCliente.Value);
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Placa))
        {
            var novaPlaca = new Placa(updateDto.Placa);
            await ValidarPlacaUnicaParaAtualizacaoAsync(novaPlaca, id);
            updateDto.Placa = novaPlaca.Valor;
        }

        _veiculoMapper.AtualizarEntidadeComDto(veiculo, updateDto);
        var veiculoAtualizado = await _veiculoRepository.UpdateAsync(veiculo);

        return _veiculoMapper.MapearParaDto(veiculoAtualizado);
    }

    public async Task<bool> RemoverVeiculoAsync(int id)
    {
        ValidarId(id);

        var veiculoExiste = await _veiculoRepository.ExistsAsync(id);
        if (!veiculoExiste)
            throw new VeiculoNaoEncontradoException(id);

        return await _veiculoRepository.DeleteAsync(id);
    }

    #endregion

    #region M�todos em ingl�s (compatibilidade)

    public async Task<IEnumerable<VeiculoDto>> GetAllAsync()
        => await ObterTodosVeiculosAsync();

    public async Task<VeiculoDto?> GetByIdAsync(int id)
        => await ObterVeiculoPorIdAsync(id);

    public async Task<IEnumerable<VeiculoDto>> GetByClienteIdAsync(int clienteId)
        => await ObterVeiculosPorClienteAsync(clienteId);

    public async Task<VeiculoDto> CreateAsync(CreateVeiculoDto createDto)
        => await CriarVeiculoAsync(createDto);

    public async Task<VeiculoDto> UpdateAsync(int id, UpdateVeiculoDto updateDto)
        => await AtualizarVeiculoAsync(id, updateDto);

    public async Task<bool> DeleteAsync(int id)
        => await RemoverVeiculoAsync(id);

    public async Task<VeiculoDto?> GetByPlacaAsync(string placa)
        => await ObterVeiculoPorPlacaAsync(placa);

    #endregion

    #region M�todos privados de valida��o

    private static void ValidarId(int id)
    {
        if (id <= 0)
            throw new ArgumentException("ID deve ser maior que zero", nameof(id));
    }

    private static void ValidarDtoCreate(CreateVeiculoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.IdCliente <= 0)
            throw new ArgumentException("ID do cliente deve ser maior que zero", nameof(dto.IdCliente));

        if (string.IsNullOrWhiteSpace(dto.Placa))
            throw new ArgumentException("Placa � obrigat�ria", nameof(dto.Placa));

        if (string.IsNullOrWhiteSpace(dto.MarcaModelo))
            throw new ArgumentException("Marca/Modelo � obrigat�rio", nameof(dto.MarcaModelo));
    }

    private static void ValidarDtoUpdate(UpdateVeiculoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.IdCliente.HasValue && dto.IdCliente <= 0)
            throw new ArgumentException("ID do cliente deve ser maior que zero", nameof(dto.IdCliente));
    }

    private async Task<Veiculo> ObterVeiculoExistenteAsync(int id)
    {
        var veiculo = await _veiculoRepository.GetByIdAsync(id);
        return veiculo ?? throw new VeiculoNaoEncontradoException(id);
    }

    private async Task ValidarClienteExisteAsync(int clienteId)
    {
        var clienteExiste = await _clienteRepository.ExistsAsync(clienteId);
        if (!clienteExiste)
            throw new ClienteNaoExisteParaVeiculoException(clienteId);
    }

    private async Task ValidarPlacaUnicaAsync(Placa placa)
    {
        var veiculoExistente = await _veiculoRepository.GetByPlacaAsync(placa.Valor);
        if (veiculoExistente != null)
            throw new PlacaJaCadastradaException(placa.ValorFormatado);
    }

    private async Task ValidarPlacaUnicaParaAtualizacaoAsync(Placa placa, int idVeiculoAtual)
    {
        var veiculoExistente = await _veiculoRepository.GetByPlacaAsync(placa.Valor);
        if (veiculoExistente != null && veiculoExistente.Id != idVeiculoAtual)
            throw new PlacaJaCadastradaException(placa.ValorFormatado);
    }

    #endregion
}