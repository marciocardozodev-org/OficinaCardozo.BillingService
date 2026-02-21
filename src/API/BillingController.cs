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
        
            [HttpPost("mercadopago/webhook")]
            [AllowAnonymous]
            public async Task<IActionResult> MercadoPagoWebhook(
                [FromQuery] string type,
                [FromQuery] string id,
                [FromHeader(Name = "x-signature")] string? signature = null)
            {
                try
                {
                    var webhookHandler = HttpContext.RequestServices
                        .GetService<OFICINACARDOZO.BILLINGSERVICE.API.Billing.MercadoPagoWebhookHandler>();
                    await webhookHandler.HandleWebhookAsync(type, id, signature);
                    return Ok(new { message = "Webhook processado com sucesso" });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { erro = "Erro ao processar webhook", detalhe = ex.Message });
                }
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
        [AllowAnonymous]  // ✅ TEMP: Para teste E2E
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

        [HttpPost("payments/{osId}/start")]
        [AllowAnonymous]  // ✅ TEMP: Para teste E2E
        public async Task<IActionResult> InitiarPagamento(
            Guid osId,
            [FromBody] PaymentInitiationRequestDto? dto = null)
        {
            try
            {
                var correlationId = dto?.CorrelationId ?? Guid.NewGuid();
                var causationId = dto?.CausationId ?? Guid.NewGuid();

                // ✅ Buscar orçamento aprovado
                var orcamento = await _orcamentoService.ObterOrcamentoPorOsIdAsync(osId);
                if (orcamento == null)
                {
                    return NotFound(new { erro = $"Nenhum orçamento encontrado para OS {osId}" });
                }

                // ✅ Verificar se está aprovado
                if (orcamento.Status != StatusOrcamento.Aprovado)
                {
                    return BadRequest(new
                    {
                        erro = "Orçamento não está aprovado",
                        statusAtual = orcamento.Status,
                        esperado = StatusOrcamento.Aprovado
                    });
                }

                // ✅ Iniciar pagamento com mock do Mercado Pago
                var pagamento = await _pagamentoService.IniciarPagamentoAsync(
                    osId,
                    orcamento.Id,
                    orcamento.Valor,
                    correlationId,
                    causationId
                );

                return Ok(new
                {
                    pagamento.Id,
                    pagamento.OsId,
                    pagamento.OrcamentoId,
                    pagamento.Valor,
                    Status = pagamento.Status.ToString(),
                    pagamento.ProviderPaymentId,
                    pagamento.CriadoEm
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = "Erro ao iniciar pagamento", detalhe = ex.Message });
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

    public class PaymentInitiationRequestDto
    {
        public Guid? CorrelationId { get; set; }
        public Guid? CausationId { get; set; }
    }
}