using Microsoft.AspNetCore.Mvc;
using OficinaCardozo.Application.DTOs;
using OficinaCardozo.Application.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Threading;
using OficinaCardozo.Application.Interfaces;

namespace OficinaCardozo.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdemServicoBatchController : ControllerBase
    {
        private readonly IOrdemServicoService _ordemServicoService;
        private readonly IClienteService _clienteService;
        private readonly IServicoService _servicoService;
        private readonly IVeiculoService _veiculoService;
        private readonly Random _random = new Random();

        public OrdemServicoBatchController(
            IOrdemServicoService ordemServicoService,
            IClienteService clienteService,
            IServicoService servicoService,
            IVeiculoService veiculoService)
        {
            _ordemServicoService = ordemServicoService;
            _clienteService = clienteService;
            _servicoService = servicoService;
            _veiculoService = veiculoService;
        }

        [HttpPost("criar-ordens-teste")] // POST /api/OrdemServicoBatch/criar-ordens-teste
        public async Task<IActionResult> CriarOrdensTeste()
        {
            var results = new List<object>();
            // Busca ou cria um serviço válido
            var servicos = await _servicoService.GetAllAsync();
            var servico = servicos.FirstOrDefault();
            if (servico == null)
            {
                var novoServico = new CreateServicoDto { NomeServico = "Serviço Teste", Preco = 100, TempoEstimadoExecucao = 1 };
                servico = await _servicoService.CreateAsync(novoServico);
            }
            for (int i = 0; i < 20; i++)
            {
                var cpf = $"999999990{i:00}";
                var placa = $"TESTE{i:00}";
                // Busca ou cria cliente
                var cliente = (await _clienteService.ObterTodosClientesAsync()).FirstOrDefault(c => c.CpfCnpj == cpf);
                if (cliente == null)
                {
                    var novoCliente = new CreateClienteDto { Nome = $"Cliente Teste {i}", CpfCnpj = cpf, EmailPrincipal = $"cliente{i}@teste.com", TelefonePrincipal = "11999990000" };
                    cliente = await _clienteService.CriarClienteAsync(novoCliente);
                }
                // Busca ou cria veículo
                var veiculo = await _veiculoService.GetByPlacaAsync(placa);
                if (veiculo == null)
                {
                    var novoVeiculo = new CreateVeiculoDto { Placa = placa, MarcaModelo = "Modelo Teste", AnoFabricacao = 2020, Cor = "Azul", TipoCombustivel = "Flex", IdCliente = cliente.Id };
                    veiculo = await _veiculoService.CreateAsync(novoVeiculo);
                }
                var createDto = new CreateOrdemServicoDto
                {
                    ClienteCpfCnpj = cpf,
                    VeiculoPlaca = placa,
                    VeiculoMarcaModelo = veiculo.MarcaModelo,
                    VeiculoAnoFabricacao = veiculo.AnoFabricacao,
                    VeiculoCor = veiculo.Cor,
                    VeiculoTipoCombustivel = veiculo.TipoCombustivel,
                    ServicosIds = new List<int> { servico.Id },
                    Pecas = new List<CreateOrdemServicoPecaDto>()
                };
                try
                {
                    var ordem = await _ordemServicoService.CreateOrdemServicoComOrcamentoAsync(createDto);
                    await Task.Delay(_random.Next(500, 2000));
                    await _ordemServicoService.IniciarDiagnosticoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    await _ordemServicoService.FinalizarDiagnosticoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    await _ordemServicoService.IniciarExecucaoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    await _ordemServicoService.FinalizarServicoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    await _ordemServicoService.EntregarVeiculoAsync(ordem.Id);
                    results.Add(new { ordem.Id, Status = "OK" });
                }
                catch (Exception ex)
                {
                    results.Add(new { Ordem = i, Error = ex.Message });
                }
            }
            return Ok(results);
        }
    }
}
