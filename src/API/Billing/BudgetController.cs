using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using OFICINACARDOZO.BILLINGSERVICE.Application;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    [ApiController]
    [Route("billing/budgets")]
    public class BudgetController : ControllerBase
    {
        private readonly OrcamentoService _orcamentoService;

        public BudgetController(OrcamentoService orcamentoService)
        {
            _orcamentoService = orcamentoService;
        }

        /// <summary>
        /// GET /billing/budgets/{osId}
        /// Busca orçamento criado para uma OS
        /// Esperado: registro de orçamento no DB de Billing
        /// </summary>
        [HttpGet("{osId}")]
        public async Task<IActionResult> GetBudget([FromRoute] Guid osId)
        {
            var budget = await _orcamentoService.GetBudgetByOsIdAsync(osId);
            if (budget == null) return NotFound(new { message = "Orçamento não encontrado para o osId informado" });
            return Ok(new { osId, budget });
        }
    }
}
