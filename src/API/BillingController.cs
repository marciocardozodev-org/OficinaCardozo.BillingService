using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using OFICINACARDOZO.BILLINGSERVICE.Domain;

namespace OFICINACARDOZO.BILLINGSERVICE.API
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BillingController : ControllerBase
    {
        private readonly OrcamentoService _orcamentoService;
        private readonly PagamentoService _pagamentoService;
        private readonly AtualizacaoStatusOsService _statusOsService;

        public BillingController(
            OrcamentoService orcamentoService, 
            PagamentoService pagamentoService, 
            AtualizacaoStatusOsService statusOsService)
        {
            _orcamentoService = orcamentoService;
            _pagamentoService = pagamentoService;
            _statusOsService = statusOsService;
        }

        [HttpPost("orcamento")]
        public async Task<IActionResult> GerarOrcamento([FromBody] OrcamentoRequestDto dto)
        {
            var orcamento = await _orcamentoService.GerarEEnviarOrcamentoAsync(
                dto.OsId, 
                dto.Valor, 
                dto.EmailCliente,
                dto.CorrelationId,
                dto.CausationId ?? Guid.NewGuid()
            );
            return Ok(orcamento);
        }

        [HttpPost("pagamento")]
        public IActionResult RegistrarPagamento([FromBody] PagamentoRequestDto dto)
        {
            var pagamento = _pagamentoService.RegistrarPagamento(
                dto.OsId, 
                dto.OrcamentoId,
                dto.Valor, 
                dto.Metodo,
                dto.CorrelationId,
                dto.CausationId ?? Guid.NewGuid()
            );
            return Ok(pagamento);
        }

        [HttpGet("pagamento/{id}")]
        public IActionResult ObterPagamento(long id)
        {
            var pagamento = _pagamentoService.ObterPagamento(id);
            if (pagamento == null) return NotFound();
            return Ok(pagamento);
        }

        [HttpPut("status-os")]
        public IActionResult AtualizarStatusOs([FromBody] AtualizacaoStatusOsDto dto)
        {
            var atualizacao = _statusOsService.AtualizarStatus(
                dto.OsId, 
                dto.NovoStatus,
                dto.EventType,
                dto.CorrelationId,
                dto.CausationId
            );
            return Ok(atualizacao);
        }

        [HttpPost("budgets/{osId}/approve")]
        public async Task<IActionResult> AprovaBudget(Guid osId, [FromBody] BudgetApprovalRequestDto? dto = null)
        {
            try
            {
                var orcamento = await _orcamentoService.AprovaBudgetAsync(
                    osId,
                    dto?.CorrelationId,
                    dto?.CausationId
                );
                return Ok(orcamento);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { erro = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { erro = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = "Erro ao aprovar orçamento", detalhe = ex.Message });
            }
        }
    }

    public class OrcamentoRequestDto
    {
        public Guid OsId { get; set; }
        public decimal Valor { get; set; }
        public string EmailCliente { get; set; } = string.Empty;
        public Guid CorrelationId { get; set; }
        public Guid? CausationId { get; set; }
    }

    public class PagamentoRequestDto
    {
        public Guid OsId { get; set; }
        public long OrcamentoId { get; set; }
        public decimal Valor { get; set; }
        public string Metodo { get; set; } = string.Empty;
        public Guid CorrelationId { get; set; }
        public Guid? CausationId { get; set; }
    }

    public class AtualizacaoStatusOsDto
    {
        public Guid OsId { get; set; }
        public string NovoStatus { get; set; } = string.Empty;
        public string? EventType { get; set; }
        public Guid? CorrelationId { get; set; }
        public Guid? CausationId { get; set; }
    }

    public class BudgetApprovalRequestDto
    {
        public Guid? CorrelationId { get; set; }
        public Guid? CausationId { get; set; }
    }
}