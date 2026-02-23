using Microsoft.EntityFrameworkCore;
using OFICINACARDOZO.BILLINGSERVICE;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;

/// <summary>
/// Fixture para criar um contexto de banco de dados em memória para testes
/// </summary>
public class BillingDbContextFixture : IDisposable
{
    private BillingDbContext? _context;

    public BillingDbContext GetContext()
    {
        if (_context != null)
            return _context;

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(databaseName: $"BillingDb_{Guid.NewGuid()}")
            .Options;

        _context = new BillingDbContext(options);
        _context.Database.EnsureCreated();
        return _context;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}

/// <summary>
/// Helper para criar entidades de domínio com valores padrão para testes
/// </summary>
public class DomainEntityBuilder
{
    public static OFICINACARDOZO.BILLINGSERVICE.Domain.Orcamento CreateOrcamento(
        Guid? osId = null,
        decimal valor = 100.00m,
        string emailCliente = "test@example.com",
        OFICINACARDOZO.BILLINGSERVICE.Domain.StatusOrcamento status = OFICINACARDOZO.BILLINGSERVICE.Domain.StatusOrcamento.Enviado)
    {
        return new OFICINACARDOZO.BILLINGSERVICE.Domain.Orcamento
        {
            OsId = osId ?? Guid.NewGuid(),
            Valor = valor,
            EmailCliente = emailCliente,
            Status = status,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };
    }

    public static OFICINACARDOZO.BILLINGSERVICE.Domain.Pagamento CreatePagamento(
        Guid? osId = null,
        long orcamentoId = 1,
        decimal valor = 100.00m,
        string metodo = "PIX",
        OFICINACARDOZO.BILLINGSERVICE.Domain.StatusPagamento status = OFICINACARDOZO.BILLINGSERVICE.Domain.StatusPagamento.Pendente)
    {
        return new OFICINACARDOZO.BILLINGSERVICE.Domain.Pagamento
        {
            OsId = osId ?? Guid.NewGuid(),
            OrcamentoId = orcamentoId,
            Valor = valor,
            Metodo = metodo,
            Status = status,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Helper para criar eventos de contrato para testes
/// </summary>
public class EventBuilder
{
    public static OFICINACARDOZO.BILLINGSERVICE.Contracts.Events.OsCreated CreateOsCreatedEvent(
        Guid? osId = null,
        decimal? valor = 150.00m)
    {
        return new OFICINACARDOZO.BILLINGSERVICE.Contracts.Events.OsCreated
        {
            OsId = osId ?? Guid.NewGuid(),
            Valor = valor
        };
    }

    public static OFICINACARDOZO.BILLINGSERVICE.Contracts.Events.EventEnvelope<T> CreateEventEnvelope<T>(
        T payload,
        Guid? correlationId = null,
        Guid? causationId = null) where T : class
    {
        return new OFICINACARDOZO.BILLINGSERVICE.Contracts.Events.EventEnvelope<T>
        {
            Payload = payload,
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CausationId = causationId ?? Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
    }
}
