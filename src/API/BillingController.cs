using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            OrcamentoService orcamentoService, 
            PagamentoService pagamentoService, 
            AtualizacaoStatusOsService statusOsService,
            ILogger<BillingController> logger)
        {
            _orcamentoService = orcamentoService;
            _pagamentoService = pagamentoService;
            _statusOsService = statusOsService;
            _logger = logger;
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
                [FromQuery] string? type = null,
                [FromQuery] string? id = null,
                [FromHeader(Name = "x-signature")] string? signature = null,
                [FromBody] MercadoPagoWebhookPayload? payload = null)
            {
                try
                {
                    // Suporta ambos os formatos:
                    // 1. Query params: ?type=payment&id=123 (testes manuais)
                    // 2. Body JSON: {"action": "payment.created", "data": {"id": 123}} (MP real)
                    
                    var webhookType = type;
                    var webhookId = id;
                    var webhookAction = payload?.Action ?? HttpContext.Request.Query["action"].ToString();

                    if (string.IsNullOrWhiteSpace(webhookAction))
                    {
                        _logger.LogWarning(
                            "Webhook sem action. Ignorando para evitar confirmacao indevida. CorrelationId: {CorrelationId}",
                            HttpContext.Request.Headers["x-correlation-id"].ToString());
                        return Ok(new { message = "Webhook ignorado: action ausente" });
                    }

                    if (!webhookAction.Equals("payment.updated", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Webhook ignorado. Action nao suportada: {Action}",
                            webhookAction);
                        return Ok(new { message = "Webhook ignorado: action nao suportada" });
                    }
                    
                    // Se body foi enviado, extrair informações dele
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.Action))
                    {
                        // Mapear action do MP para nosso formato (payment.created -> payment)
                        webhookType = payload.Action.StartsWith("payment", StringComparison.OrdinalIgnoreCase)
                            ? "payment"
                            : payload.Action;
                        
                        if (payload.Data?.Id != null)
                        {
                            webhookId = payload.Data.Id.ToString();
                        }
                    }
                    
                    // Fallback: tentar extrair ID de data.id query param
                    if (string.IsNullOrWhiteSpace(webhookId))
                    {
                        webhookId = HttpContext.Request.Query["data.id"].ToString();
                    }

                    var webhookHandler = HttpContext.RequestServices
                        .GetService<OFICINACARDOZO.BILLINGSERVICE.API.Billing.MercadoPagoWebhookHandler>();
                    
                    await webhookHandler.HandleWebhookAsync(webhookType, webhookId, signature);
                    return Ok(new { message = "Webhook processado com sucesso" });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { erro = "Erro ao processar webhook", detalhe = ex.Message });
                }
            }

            [HttpGet("mercadopago/webhook")]
            [AllowAnonymous]
            public IActionResult MercadoPagoWebhookHealth()
            {
                return Ok(new { message = "Webhook endpoint ativo" });
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

        /// <summary>
        /// GET /api/billing/outbox - Lista todos os eventos no outbox
        /// </summary>
        [AllowAnonymous]
        [HttpGet("outbox")]
        public async Task<IActionResult> GetOutbox(
            [FromServices] BillingDbContext db)
        {
            try
            {
                var events = await db.OutboxMessages
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(50)
                    .Select(o => new
                    {
                        o.Id,
                        o.EventType,
                        o.Published,
                        o.CreatedAt,
                        o.PublishedAt,
                        o.CorrelationId,
                        PayloadPreview = o.Payload.Length > 200 ? 
                            o.Payload.Substring(0, 200) + "..." : o.Payload
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Total = events.Count,
                    Events = events
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = ex.Message });
            }
        }

        /// <summary>
        /// DTO para payload do webhook do Mercado Pago
        /// Formato: {"action": "payment.created", "data": {"id": 123}}
        /// </summary>
        public class MercadoPagoWebhookPayload
        {
            [System.Text.Json.Serialization.JsonPropertyName("action")]
            public string? Action { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("data")]
            public WebhookData? Data { get; set; }
        }

        public class WebhookData
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public long? Id { get; set; }
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