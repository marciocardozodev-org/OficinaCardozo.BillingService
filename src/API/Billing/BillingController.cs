using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    [ApiController]
    [Route("billing")]
    public class BillingController : ControllerBase
    {
        [HttpPost("budgets/{osId}/approve")]
        public async Task<IActionResult> ApproveBudget([FromRoute] string osId)
        {
            // Aprovação simulada, publicar BudgetApproved via Outbox
            return Ok();
        }

        [HttpPost("payments/{osId}/start")]
        public async Task<IActionResult> StartPayment([FromRoute] string osId)
        {
            // Iniciar pagamento Mercado Pago, publicar PaymentPending
            return Ok();
        }

        [HttpPost("mercadopago/webhook")]
        public async Task<IActionResult> MercadoPagoWebhook()
        {
            // Processar webhook, validar idempotência, publicar PaymentConfirmed/Failed
            return Ok();
        }
    }
}
