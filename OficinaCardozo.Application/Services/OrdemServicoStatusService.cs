using Microsoft.Extensions.Logging;
using OficinaCardozo.Application.DTOs;
using OficinaCardozo.Domain.Interfaces;

namespace OficinaCardozo.Application.Services;

public interface IOrdemServicoStatusService
{
    Task<OrdemServicoDto> AtualizarStatusViaEmailAsync(ComandoEmailDto comando);
    Task<bool> ValidarTransicaoStatusAsync(int ordemServicoId, string statusAtual, string novoStatus);
}

public class OrdemServicoStatusService : IOrdemServicoStatusService
{
    private readonly IOrdemServicoRepository _ordemServicoRepository;
    private readonly IOrdemServicoStatusRepository _statusRepository;
    private readonly ILogger<OrdemServicoStatusService> _logger;

    public OrdemServicoStatusService(
        IOrdemServicoRepository ordemServicoRepository,
        IOrdemServicoStatusRepository statusRepository,
        ILogger<OrdemServicoStatusService> logger)
    {
        _ordemServicoRepository = ordemServicoRepository;
        _statusRepository = statusRepository;
        _logger = logger;
    }

    public async Task<OrdemServicoDto> AtualizarStatusViaEmailAsync(ComandoEmailDto comando)
    {
        _logger.LogInformation("?? Iniciando atualiza��o de status via email para OS #{OrdemId}", comando.OrdemServicoId);

        try
        {
            var ordem = await _ordemServicoRepository.GetByIdWithDetailsAsync(comando.OrdemServicoId);
            if (ordem == null)
            {
                StatsdClient.Metrics.Counter("ordem_servico.fail", 1);
                throw new KeyNotFoundException($"Ordem de servi�o #{comando.OrdemServicoId} n�o encontrada");
            }

            var novoStatus = await _statusRepository.GetByDescricaoAsync(comando.NovoStatus);
            if (novoStatus == null)
            {
                StatsdClient.Metrics.Counter("ordem_servico.fail", 1);
                throw new InvalidOperationException($"Status '{comando.NovoStatus}' n�o encontrado no sistema");
            }

            var statusAtual = ordem.Status?.Descricao ?? "";
            var transicaoValida = await ValidarTransicaoStatusAsync(comando.OrdemServicoId, statusAtual, comando.NovoStatus);

            if (!transicaoValida)
            {
                _logger.LogWarning("?? Transi��o de status n�o permitida: {StatusAtual} -> {NovoStatus}",
                    statusAtual, comando.NovoStatus);
                _logger.LogInformation("?? Modo acad�mico: Permitindo transi��o para demonstra��o");
            }

            ordem.IdStatus = novoStatus.Id;
            AtualizarDatasPorStatus(ordem, comando.NovoStatus);
            await _ordemServicoRepository.UpdateAsync(ordem);
            _logger.LogInformation("? Status atualizado: OS #{OrdemId} {StatusAntigo} -> {StatusNovo}",
                comando.OrdemServicoId, statusAtual, comando.NovoStatus);
            var ordemAtualizada = await _ordemServicoRepository.GetByIdWithDetailsAsync(comando.OrdemServicoId);
            return MapToDto(ordemAtualizada!);
        }
        catch (Exception ex)
        {
            StatsdClient.Metrics.Counter("ordem_servico.fail", 1);
            _logger.LogError(ex, "Falha ao processar ordem de serviço");
            throw;
        }
    }

    public async Task<bool> ValidarTransicaoStatusAsync(int ordemServicoId, string statusAtual, string novoStatus)
    {
        var transicoesValidas = new Dictionary<string, string[]>
        {
            [OrdemServicoStatusConstants.RECEBIDA] = [
                OrdemServicoStatusConstants.EM_DIAGNOSTICO,
                OrdemServicoStatusConstants.CANCELADA
            ],
            [OrdemServicoStatusConstants.EM_DIAGNOSTICO] = [
                OrdemServicoStatusConstants.EM_ELABORACAO,
                OrdemServicoStatusConstants.CANCELADA
            ],
            [OrdemServicoStatusConstants.EM_ELABORACAO] = [
                OrdemServicoStatusConstants.AGUARDANDO_APROVACAO,
                OrdemServicoStatusConstants.CANCELADA
            ],
            [OrdemServicoStatusConstants.AGUARDANDO_APROVACAO] = [
                OrdemServicoStatusConstants.EM_EXECUCAO,
                OrdemServicoStatusConstants.EM_ELABORACAO,
                OrdemServicoStatusConstants.CANCELADA,
                OrdemServicoStatusConstants.DEVOLVIDA
            ],
            [OrdemServicoStatusConstants.EM_EXECUCAO] = [
                OrdemServicoStatusConstants.FINALIZADA
            ],
            [OrdemServicoStatusConstants.FINALIZADA] = [
                OrdemServicoStatusConstants.ENTREGUE
            ],
            [OrdemServicoStatusConstants.CANCELADA] = [
                OrdemServicoStatusConstants.DEVOLVIDA,
                OrdemServicoStatusConstants.RECEBIDA
            ]
        };

        if (transicoesValidas.TryGetValue(statusAtual, out var statusesPermitidos))
        {
            return statusesPermitidos.Contains(novoStatus);
        }

        return await Task.FromResult(true);
    }

    private static void AtualizarDatasPorStatus(Domain.Entities.OrdemServico ordem, string novoStatus)
    {
        switch (novoStatus)
        {
            case OrdemServicoStatusConstants.FINALIZADA:
                ordem.DataFinalizacao = DateTime.Now;
                break;

            case OrdemServicoStatusConstants.ENTREGUE:
            case OrdemServicoStatusConstants.DEVOLVIDA:
                ordem.DataEntrega = DateTime.Now;
                if (!ordem.DataFinalizacao.HasValue)
                {
                    ordem.DataFinalizacao = DateTime.Now;
                }
                break;
        }
    }

    private static OrdemServicoDto MapToDto(Domain.Entities.OrdemServico ordemServico)
    {
        return new OrdemServicoDto
        {
            Id = ordemServico.Id,
            DataSolicitacao = ordemServico.DataSolicitacao,
            IdVeiculo = ordemServico.IdVeiculo,
            IdStatus = ordemServico.IdStatus,
            StatusDescricao = ordemServico.Status?.Descricao,
            DataFinalizacao = ordemServico.DataFinalizacao,
            DataEntrega = ordemServico.DataEntrega,
            VeiculoPlaca = ordemServico.Veiculo?.Placa,
            VeiculoMarcaModelo = ordemServico.Veiculo?.MarcaModelo,
            ClienteNome = ordemServico.Veiculo?.Cliente?.Nome,
            ClienteEmail = ordemServico.Veiculo?.Cliente?.EmailPrincipal
        };
    }
}