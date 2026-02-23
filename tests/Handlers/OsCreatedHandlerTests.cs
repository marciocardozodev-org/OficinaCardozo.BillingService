using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Handlers;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;
using OFICINACARDOZO.BILLINGSERVICE.API.Billing;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Handlers;

public class OsCreatedHandlerTests : IDisposable
{
    private readonly BillingDbContextFixture _dbFixture;
    private readonly OrcamentoService _orcamentoService;
    private readonly Mock<PagamentoService> _mockPagamentoService;
    private readonly Mock<ILogger<OsCreatedHandler>> _mockLogger;

    public OsCreatedHandlerTests()
    {
        _dbFixture = new BillingDbContextFixture();
        _orcamentoService = new OrcamentoService(_dbFixture.GetContext());
        _mockPagamentoService = new Mock<PagamentoService>(
            _dbFixture.GetContext(),
            Mock.Of<IMercadoPagoService>(),
            Mock.Of<ILogger<PagamentoService>>());
        _mockLogger = new Mock<ILogger<OsCreatedHandler>>();
    }

    public void Dispose()
    {
        _dbFixture.Dispose();
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WithNewOsCreated_ShouldCreateOrçamento()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var valor = 150.00m;
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, valor);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var context = _dbFixture.GetContext();
        var orcamento = await context.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        orcamento.Should().NotBeNull();
        orcamento!.Valor.Should().Be(valor);
        orcamento.Status.Should().Be(StatusOrcamento.Enviado);
    }

    [Fact]
    public async Task HandleAsync_WithoutValorInEvent_ShouldUseFallbackValue()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, null);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var context = _dbFixture.GetContext();
        var orcamento = await context.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        orcamento.Should().NotBeNull();
        orcamento!.Valor.Should().Be(100.00m);  // Default value
    }

    [Fact]
    public async Task HandleAsync_WithZeroValor_ShouldUseFallbackValue()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, 0.00m);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var context = _dbFixture.GetContext();
        var orcamento = await context.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        orcamento!.Valor.Should().Be(100.00m);  // Default value
    }

    [Fact]
    public async Task HandleAsync_WithNegativeValor_ShouldUseFallbackValue()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, -50.00m);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var context = _dbFixture.GetContext();
        var orcamento = await context.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        orcamento!.Valor.Should().Be(100.00m);  // Default value
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateOutboxMessageForBudgetGenerated()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, 200.00m);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var context = _dbFixture.GetContext();
        var handler = new OsCreatedHandler(
            context,
            new OrcamentoService(context),
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var outboxMessages = await context.OutboxMessages.ToListAsync();
        outboxMessages.Should().NotBeEmpty();
        outboxMessages.Should().Contain(m => m.EventType == nameof(BudgetGenerated));
    }

    [Fact]
    public async Task HandleAsync_ShouldApproveOrcamentoAutomatically()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, 100.00m);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var mockPagamentoService = new Mock<PagamentoService>(
            _dbFixture.GetContext(),
            Mock.Of<IMercadoPagoService>(),
            Mock.Of<ILogger<PagamentoService>>());

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var context = _dbFixture.GetContext();
        var orcamento = await context.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        orcamento!.Status.Should().Be(StatusOrcamento.Aprovado);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallPagamentoServiceWhenOrcamentoApproved()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, 150.00m);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var context = _dbFixture.GetContext();
        var orcamento = await context.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        orcamento.Should().NotBeNull();
        orcamento!.Status.Should().Be(StatusOrcamento.Aprovado);
    }

    [Fact]
    public async Task HandleAsync_WithExistingOrcamento_ShouldNotCreateDuplicate()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var existingOrcamento = DomainEntityBuilder.CreateOrcamento(osId, 100.00m);
        var context = _dbFixture.GetContext();
        context.Orcamentos.Add(existingOrcamento);
        await context.SaveChangesAsync();

        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, 200.00m);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            new OrcamentoService(_dbFixture.GetContext()),
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var orcamentos = await context.Orcamentos
            .Where(o => o.OsId == osId)
            .ToListAsync();
        orcamentos.Should().HaveCount(1);
        orcamentos[0].Valor.Should().Be(100.00m);  // Should keep existing value
    }

    [Fact]
    public async Task HandleAsync_ShouldPreserveCorrelationId()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var customCorrelationId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, 100.00m);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated, customCorrelationId);

        var context = _dbFixture.GetContext();
        var handler = new OsCreatedHandler(
            context,
            new OrcamentoService(context),
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        var orcamento = await context.Orcamentos.FirstOrDefaultAsync(o => o.OsId == osId);
        orcamento!.CorrelationId.Should().Be(customCorrelationId);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task HandleAsync_ShouldLogOsCreatedEvent()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("BillingService consumiu evento OsCreated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithFallbackValue_ShouldLogWarning()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = EventBuilder.CreateOsCreatedEvent(osId, null);
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        var handler = new OsCreatedHandler(
            _dbFixture.GetContext(),
            _orcamentoService,
            _mockPagamentoService.Object,
            _mockLogger.Object);

        // Act
        await handler.HandleAsync(envelope);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Valor não fornecido")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
