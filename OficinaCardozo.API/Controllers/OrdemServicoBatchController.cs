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
            Console.WriteLine($"[OrdemServicoBatchController] Iniciando execução do endpoint criar-ordens-teste em {DateTime.UtcNow:O}");
            var results = new List<object>();
            // Busca ou cria um serviço válido
            Console.WriteLine($"[OrdemServicoBatchController] Buscando serviço de teste...");
            var servicos = await _servicoService.GetAllAsync();
            var servico = servicos.FirstOrDefault();
            if (servico == null)
            {
                Console.WriteLine($"[OrdemServicoBatchController] Serviço de teste não encontrado, criando novo serviço...");
                var novoServico = new CreateServicoDto { NomeServico = "Serviço Teste", Preco = 100, TempoEstimadoExecucao = 1 };
                servico = await _servicoService.CreateAsync(novoServico);
                Console.WriteLine($"[OrdemServicoBatchController] Serviço criado com ID: {servico.Id}");
            }
            else
            {
                Console.WriteLine($"[OrdemServicoBatchController] Serviço de teste encontrado com ID: {servico.Id}");
            }
            for (int i = 0; i < 20; i++)
            {
                Console.WriteLine($"[OrdemServicoBatchController] --- Iniciando batch {i} ---");
                var cpf = $"999999990{i:00}";
                var placa = $"TESTE{i:00}";
                // Busca ou cria cliente
                Console.WriteLine($"[OrdemServicoBatchController] Buscando cliente CPF {cpf}...");
                var cliente = (await _clienteService.ObterTodosClientesAsync()).FirstOrDefault(c => c.CpfCnpj == cpf);
                if (cliente == null)
                {
                    Console.WriteLine($"[OrdemServicoBatchController] Cliente não encontrado, criando novo cliente...");
                    var novoCliente = new CreateClienteDto { Nome = $"Cliente Teste {i}", CpfCnpj = cpf, EmailPrincipal = $"cliente{i}@teste.com", TelefonePrincipal = "11999990000" };
                    cliente = await _clienteService.CriarClienteAsync(novoCliente);
                    Console.WriteLine($"[OrdemServicoBatchController] Cliente criado com ID: {cliente.Id}");
                }
                else
                {
                    Console.WriteLine($"[OrdemServicoBatchController] Cliente encontrado com ID: {cliente.Id}");
                }
                // Busca ou cria veículo
                Console.WriteLine($"[OrdemServicoBatchController] Buscando veículo placa {placa}...");
                var veiculo = await _veiculoService.GetByPlacaAsync(placa);
                if (veiculo == null)
                {
                    Console.WriteLine($"[OrdemServicoBatchController] Veículo não encontrado, criando novo veículo...");
                    var novoVeiculo = new CreateVeiculoDto { Placa = placa, MarcaModelo = "Modelo Teste", AnoFabricacao = 2020, Cor = "Azul", TipoCombustivel = "Flex", IdCliente = cliente.Id };
                    veiculo = await _veiculoService.CreateAsync(novoVeiculo);
                    Console.WriteLine($"[OrdemServicoBatchController] Veículo criado com ID: {veiculo.Id}");
                }
                else
                {
                    Console.WriteLine($"[OrdemServicoBatchController] Veículo encontrado com ID: {veiculo.Id}");
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
                    Console.WriteLine($"[OrdemServicoBatchController] Criando ordem de serviço para cliente {cliente.Id}, veículo {veiculo.Id}...");
                    var ordem = await _ordemServicoService.CreateOrdemServicoComOrcamentoAsync(createDto);
                    Console.WriteLine($"[OrdemServicoBatchController] Ordem criada com ID: {ordem.Id}");
                    await Task.Delay(_random.Next(500, 2000));
                    Console.WriteLine($"[OrdemServicoBatchController] Iniciando diagnóstico da ordem {ordem.Id}...");
                    await _ordemServicoService.IniciarDiagnosticoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    Console.WriteLine($"[OrdemServicoBatchController] Finalizando diagnóstico da ordem {ordem.Id}...");
                    await _ordemServicoService.FinalizarDiagnosticoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    Console.WriteLine($"[OrdemServicoBatchController] Iniciando execução da ordem {ordem.Id}...");
                    await _ordemServicoService.IniciarExecucaoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    Console.WriteLine($"[OrdemServicoBatchController] Finalizando serviço da ordem {ordem.Id}...");
                    await _ordemServicoService.FinalizarServicoAsync(ordem.Id);
                    await Task.Delay(_random.Next(500, 2000));
                    Console.WriteLine($"[OrdemServicoBatchController] Entregando veículo da ordem {ordem.Id}...");
                    await _ordemServicoService.EntregarVeiculoAsync(ordem.Id);
                    results.Add(new { ordem.Id, Status = "OK" });
                    Console.WriteLine($"[OrdemServicoBatchController] --- Batch {i} finalizado com sucesso ---");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OrdemServicoBatchController] Erro no batch {i}: {ex.Message}");
                    results.Add(new { Ordem = i, Error = ex.Message });
                }
            }
            Console.WriteLine($"[OrdemServicoBatchController] Finalizou execução do endpoint criar-ordens-teste em {DateTime.UtcNow:O}");
            return Ok(results);
        }
    }
}
