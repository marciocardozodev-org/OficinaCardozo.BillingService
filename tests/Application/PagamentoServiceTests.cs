using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using OFICINACARDOZO.BILLINGSERVICE.API.Billing;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Application;

public class PagamentoServiceTests : IDisposable
{
    private readonly BillingDbContextFixture _dbFixture;
    private readonly Mock<IMercadoPagoService> _mockMercadoPago;
    private readonly Mock<ILogger<PagamentoService>> _mockLogger;
    private readonly PagamentoService _service;

    public PagamentoServiceTests()
    {
        _dbFixture = new BillingDbContextFixture();
        _mockMercadoPago = new Mock<IMercadoPagoService>();
        _mockLogger = new Mock<ILogger<PagamentoService>>();
        _service = new PagamentoService(
            _dbFixture.GetContext(),
            _mockMercadoPago.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbFixture.Dispose();
    }

    #region IniciarPagamentoAsync Tests

    [Fact]
    public async Task IniciarPagamentoAsync_WithValidData_ShouldCreatePayment()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamentoId = 1L;
        var valor = 150.00m;
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        _mockMercadoPago
            .Setup(m => m.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("payment_123");

        // Act
        var result = await _service.IniciarPagamentoAsync(
            osId, orcamentoId, valor, correlationId, causationId);

        // Assert
        result.Should().NotBeNull();
        result.OsId.Should().Be(osId);
        result.OrcamentoId.Should().Be(orcamentoId);
        result.Valor.Should().Be(valor);
        result.Status.Should().Be(StatusPagamento.Pendente);
        result.CorrelationId.Should().Be(correlationId);
        result.CausationId.Should().Be(causationId);
    }

    [Fact]
    public async Task IniciarPagamentoAsync_ShouldCallMercadoPagoService()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamentoId = 1L;
        var valor = 100.00m;

        _mockMercadoPago
            .Setup(m => m.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("payment_456");

        // Act
        await _service.IniciarPagamentoAsync(
            osId, orcamentoId, valor, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        _mockMercadoPago.Verify(
            m => m.InitiatePaymentAsync(osId, orcamentoId, valor, "PIX", null),
            Times.Once);
    }

    [Fact]
    public async Task IniciarPagamentoAsync_PaymentShouldBePersisted()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamentoId = 1L;
        var valor = 200.00m;

        _mockMercadoPago
            .Setup(m => m.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("payment_789");

        // Act
        var result = await _service.IniciarPagamentoAsync(
            osId, orcamentoId, valor, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        var context = _dbFixture.GetContext();
        var savedPayment = await context.Pagamentos
            .FirstOrDefaultAsync(p => p.OsId == osId);

        savedPayment.Should().NotBeNull();
        savedPayment!.Valor.Should().Be(valor);
    }

    [Theory]
    [InlineData(50.00)]
    [InlineData(100.00)]
    [InlineData(500.00)]
    [InlineData(1000.00)]
    public async Task IniciarPagamentoAsync_WithVariousValues_ShouldHandleCorrectly(
        decimal valor)
    {
        // Arrange
        _mockMercadoPago
            .Setup(m => m.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("payment_id");

        // Act
        var result = await _service.IniciarPagamentoAsync(
            Guid.NewGuid(), 1L, valor, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Valor.Should().Be(valor);
    }

    [Fact]
    public async Task IniciarPagamentoAsync_WithZeroValue_ShouldCreatePayment()
    {
        // Arrange
        _mockMercadoPago
            .Setup(m => m.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("payment_id");

        // Act
        var result = await _service.IniciarPagamentoAsync(
            Guid.NewGuid(), 1L, 0.00m, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Valor.Should().Be(0.00m);
    }

    [Fact]
    public async Task IniciarPagamentoAsync_ShouldCreateOutboxMessages()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamentoId = 1L;
        var valor = 150.00m;

        _mockMercadoPago
            .Setup(m => m.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("payment_123");

        var context = _dbFixture.GetContext();
        var service = new PagamentoService(context, _mockMercadoPago.Object, _mockLogger.Object);

        // Act
        await service.IniciarPagamentoAsync(osId, orcamentoId, valor, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        var outboxMessages = await context.OutboxMessages.ToListAsync();
        outboxMessages.Should().NotBeEmpty();
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task IniciarPagamentoAsync_ShouldLogPaymentInitiation()
    {
        // Arrange
        var osId = Guid.NewGuid();

        _mockMercadoPago
            .Setup(m => m.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("payment_id");

        // Act
        await _service.IniciarPagamentoAsync(
            osId, 1L, 100.00m, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Iniciando pagamento")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
