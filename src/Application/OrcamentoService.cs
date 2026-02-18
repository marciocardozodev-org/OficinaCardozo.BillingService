using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OFICINACARDOZO.BILLINGSERVICE.Application
{
    public class OrcamentoService
    {
        private readonly BillingDbContext _db;
        public OrcamentoService(BillingDbContext db)
        {
            _db = db;
        }

        public async Task<Orcamento> GerarEEnviarOrcamentoAsync(Guid osId, decimal valor, string emailCliente, Guid correlationId, Guid causationId)
        {
            var orcamento = new Orcamento
            {
                OsId = osId,
                Valor = valor,
                EmailCliente = emailCliente,
                Status = StatusOrcamento.Enviado,
                CorrelationId = correlationId,
                CausationId = causationId,
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            };
            _db.Orcamentos.Add(orcamento);
            await _db.SaveChangesAsync();
            return orcamento;
        }

        public async Task<Orcamento?> GetBudgetByOsIdAsync(Guid osId)
        {
            return await _db.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        }

        public async Task<Orcamento> AprovaBudgetAsync(Guid osId, Guid? correlationId = null, Guid? causationId = null)
        {
            var orcamento = await GetBudgetByOsIdAsync(osId);
            if (orcamento == null)
                throw new KeyNotFoundException($"Orçamento não encontrado para OsId: {osId}");

            if (orcamento.Status != StatusOrcamento.Enviado)
                throw new InvalidOperationException($"Orçamento deve estar em status 'Enviado' para ser aprovado. Status atual: {orcamento.Status}");

            // Atualizar status do orçamento via ExecuteUpdateAsync (executa direto no banco)
            var approvedAt = DateTime.UtcNow;
            var approvedAtUtc = DateTime.SpecifyKind(approvedAt, DateTimeKind.Utc);

            // Propagar correlation_id se não fornecido
            var correlation = correlationId ?? orcamento.CorrelationId;
            var causation = causationId ?? Guid.NewGuid();

            // Criar evento BudgetApproved
            var budgetApprovedEvent = new BudgetApproved
            {
                OrcamentoId = orcamento.Id,
                OsId = orcamento.OsId,
                Valor = orcamento.Valor,
                Status = "Aprovado",
                ApprovedAt = approvedAtUtc,
                CorrelationId = correlation,
                CausationId = causation
            };

            // Fazer UPDATE direto no banco via ExecuteUpdateAsync
            await _db.Orcamentos
                .Where(o => o.Id == orcamento.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, StatusOrcamento.Aprovado)
                    .SetProperty(o => o.AtualizadoEm, approvedAtUtc));

            // Depois adicionar OutboxMessage em uma operação separada
            var outboxMessage = new OutboxMessage
            {
                AggregateId = orcamento.Id,
                EventType = "BudgetApproved",
                Payload = JsonSerializer.Serialize(budgetApprovedEvent),
                CorrelationId = correlation,
                CausationId = causation,
                CreatedAt = approvedAtUtc,
                Published = false
            };

            _db.OutboxMessages.Add(outboxMessage);
            await _db.SaveChangesAsync();

            // Atualizar valores locais para retornar
            orcamento.Status = StatusOrcamento.Aprovado;
            orcamento.AtualizadoEm = approvedAtUtc;

            return orcamento;
        }
    }
}