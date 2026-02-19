using System;
using System.Threading.Tasks;

namespace OFICINACARDOZO.BILLINGSERVICE.API.Billing
{
    public interface IMercadoPagoService
    {
        /// <summary>
        /// Inicia um pagamento no Mercado Pago (ou mock)
        /// Retorna o ID do pagamento no provedor ou null se falhar
        /// </summary>
        Task<string?> InitiatePaymentAsync(
            Guid osId,
            long orcamentoId,
            decimal valor,
            string metodo = "CREDIT_CARD",
            string? description = null);
    }
}
