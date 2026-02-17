using System.Threading.Tasks;
using OficinaCardozo.BillingService.Contracts.Events;
using OficinaCardozo.BillingService.Messaging;

namespace OficinaCardozo.BillingService.Handlers
{
    public class OsCreatedHandler
    {
        public async Task HandleAsync(EventEnvelope<OsCreated> envelope)
        {
            // Criar orçamento local, salvar no DB, publicar BudgetGenerated via Outbox
        }
    }
}
