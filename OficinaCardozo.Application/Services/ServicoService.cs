using OficinaCardozo.Application.DTOs;
using OficinaCardozo.Application.Interfaces;
using OficinaCardozo.Domain.Entities;
using OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.Domain.ValueObjects;
using OficinaCardozo.Domain.Exceptions;

namespace OficinaCardozo.Application.Services;

public class ServicoService : IServicoService
{
    private readonly IServicoRepository _servicoRepository;
    private readonly IServicoMapper _servicoMapper;

    public ServicoService(
        IServicoRepository servicoRepository,
        IServicoMapper servicoMapper)
    {
        _servicoRepository = servicoRepository ?? throw new ArgumentNullException(nameof(servicoRepository));
        _servicoMapper = servicoMapper ?? throw new ArgumentNullException(nameof(servicoMapper));
    }

    #region M�todos em portugu�s (Clean Architecture)

    public async Task<IEnumerable<ServicoDto>> ObterTodosServicosAsync()
    {
        var servicos = await _servicoRepository.GetAllAsync();
        return _servicoMapper.MapearParaListaDto(servicos);
    }

    public async Task<ServicoDto?> ObterServicoPorIdAsync(int id)
    {
        ValidarId(id);

        var servico = await _servicoRepository.GetByIdAsync(id);
        return servico != null ? _servicoMapper.MapearParaDto(servico) : null;
    }

    public async Task<IEnumerable<ServicoDto>> BuscarServicosPorNomeAsync(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome para busca n�o pode estar vazio", nameof(nome));

        var servicos = await _servicoRepository.GetAllAsync();
        var servicosFiltrados = servicos.Where(s =>
            s.NomeServico.Contains(nome, StringComparison.OrdinalIgnoreCase));

        return _servicoMapper.MapearParaListaDto(servicosFiltrados);
    }

    public async Task<ServicoDto> CriarServicoAsync(CreateServicoDto createDto)
    {
        ValidarDtoCreate(createDto);

        var preco = new Preco(createDto.Preco);
        await ValidarNomeServicoUnicoAsync(createDto.NomeServico);

        var novoServico = _servicoMapper.MapearParaEntidade(createDto);
        novoServico.Preco = preco.Valor;

        var servicoCriado = await _servicoRepository.CreateAsync(novoServico);
        return _servicoMapper.MapearParaDto(servicoCriado);
    }

    public async Task<ServicoDto> AtualizarServicoAsync(int id, UpdateServicoDto updateDto)
    {
        ValidarId(id);
        ValidarDtoUpdate(updateDto);

        var servico = await ObterServicoExistenteAsync(id);

        if (!string.IsNullOrWhiteSpace(updateDto.NomeServico))
        {
            await ValidarNomeServicoUnicoParaAtualizacaoAsync(updateDto.NomeServico, id);
        }

        if (updateDto.Preco.HasValue)
        {
            var preco = new Preco(updateDto.Preco.Value);
            updateDto.Preco = preco.Valor;
        }

        _servicoMapper.AtualizarEntidadeComDto(servico, updateDto);
        var servicoAtualizado = await _servicoRepository.UpdateAsync(servico);

        return _servicoMapper.MapearParaDto(servicoAtualizado);
    }

    public async Task<bool> RemoverServicoAsync(int id)
    {
        ValidarId(id);

        var servicoExiste = await _servicoRepository.ExistsAsync(id);
        if (!servicoExiste)
            throw new ServicoNaoEncontradoException(id);

        return await _servicoRepository.DeleteAsync(id);
    }

    #endregion

    #region M�todos em ingl�s (compatibilidade)

    public async Task<IEnumerable<ServicoDto>> GetAllAsync()
        => await ObterTodosServicosAsync();

    public async Task<ServicoDto?> GetByIdAsync(int id)
        => await ObterServicoPorIdAsync(id);

    public async Task<ServicoDto> CreateAsync(CreateServicoDto createDto)
        => await CriarServicoAsync(createDto);

    public async Task<ServicoDto> UpdateAsync(int id, UpdateServicoDto updateDto)
        => await AtualizarServicoAsync(id, updateDto);

    public async Task<bool> DeleteAsync(int id)
        => await RemoverServicoAsync(id);

    #endregion

    #region M�todos privados de valida��o

    private static void ValidarId(int id)
    {
        if (id <= 0)
            throw new ArgumentException("ID deve ser maior que zero", nameof(id));
    }

    private static void ValidarDtoCreate(CreateServicoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.NomeServico))
            throw new ArgumentException("Nome do servi�o � obrigat�rio", nameof(dto.NomeServico));

        if (dto.Preco <= 0)
            throw new ArgumentException("Pre�o deve ser maior que zero", nameof(dto.Preco));

        if (dto.TempoEstimadoExecucao <= 0)
            throw new ArgumentException("Tempo estimado deve ser maior que zero", nameof(dto.TempoEstimadoExecucao));
    }

    private static void ValidarDtoUpdate(UpdateServicoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Preco.HasValue && dto.Preco <= 0)
            throw new ArgumentException("Pre�o deve ser maior que zero", nameof(dto.Preco));

        if (dto.TempoEstimadoExecucao.HasValue && dto.TempoEstimadoExecucao <= 0)
            throw new ArgumentException("Tempo estimado deve ser maior que zero", nameof(dto.TempoEstimadoExecucao));
    }

    private async Task<Servico> ObterServicoExistenteAsync(int id)
    {
        var servico = await _servicoRepository.GetByIdAsync(id);
        return servico ?? throw new ServicoNaoEncontradoException(id);
    }

    private async Task ValidarNomeServicoUnicoAsync(string nomeServico)
    {
        if (await _servicoRepository.ExistsByNomeAsync(nomeServico))
            throw new NomeServicoJaCadastradoException(nomeServico);
    }

    private async Task ValidarNomeServicoUnicoParaAtualizacaoAsync(string nomeServico, int idServicoAtual)
    {
        if (await _servicoRepository.ExistsByNomeAsync(nomeServico, idServicoAtual))
            throw new NomeServicoJaCadastradoException(nomeServico);
    }

    #endregion
}