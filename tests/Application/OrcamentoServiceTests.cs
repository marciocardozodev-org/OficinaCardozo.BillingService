using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Application;

public class OrcamentoServiceTests : IDisposable
{
    private readonly BillingDbContextFixture _dbFixture;
    private readonly OrcamentoService _service;

    public OrcamentoServiceTests()
    {
        _dbFixture = new BillingDbContextFixture();
        _service = new OrcamentoService(_dbFixture.GetContext());
    }

    public void Dispose()
    {
        _dbFixture.Dispose();
    }

    #region GerarEEnviarOrcamentoAsync Tests

    [Fact]
    public async Task GerarEEnviarOrcamentoAsync_WithValidData_ShouldCreateOrcamento()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var valor = 150.00m;
        var emailCliente = "cliente@test.com";
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        // Act
        var result = await _service.GerarEEnviarOrcamentoAsync(
            osId, valor, emailCliente, correlationId, causationId);

        // Assert
        result.Should().NotBeNull();
        result.OsId.Should().Be(osId);
        result.Valor.Should().Be(valor);
        result.EmailCliente.Should().Be(emailCliente);
        result.Status.Should().Be(StatusOrcamento.Enviado);
        result.CorrelationId.Should().Be(correlationId);
        result.CausationId.Should().Be(causationId);
    }

    [Fact]
    public async Task GerarEEnviarOrcamentoAsync_WithZeroValue_ShouldCreateOrcamento()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var valor = 0.00m;

        // Act
        var result = await _service.GerarEEnviarOrcamentoAsync(
            osId, valor, "test@test.com", Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Valor.Should().Be(0.00m);
    }

    [Fact]
    public async Task GerarEEnviarOrcamentoAsync_WithNegativeValue_ShouldCreateOrcamento()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var valor = -50.00m;

        // Act
        var result = await _service.GerarEEnviarOrcamentoAsync(
            osId, valor, "test@test.com", Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Valor.Should().Be(-50.00m);
    }

    [Fact]
    public async Task GerarEEnviarOrcamentoAsync_WithEmptyEmail_ShouldCreateOrcamento()
    {
        // Arrange
        var osId = Guid.NewGuid();

        // Act
        var result = await _service.GerarEEnviarOrcamentoAsync(
            osId, 100.00m, "", Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.EmailCliente.Should().Be("");
    }

    [Fact]
    public async Task GerarEEnviarOrcamentoAsync_MultipleCalls_ShouldPersistToDB()
    {
        // Arrange
        var osId1 = Guid.NewGuid();
        var osId2 = Guid.NewGuid();

        // Act
        var orcamento1 = await _service.GerarEEnviarOrcamentoAsync(
            osId1, 100.00m, "test1@test.com", Guid.NewGuid(), Guid.NewGuid());
        var orcamento2 = await _service.GerarEEnviarOrcamentoAsync(
            osId2, 200.00m, "test2@test.com", Guid.NewGuid(), Guid.NewGuid());

        // Assert
        var context = _dbFixture.GetContext();
        var orcamentos = await context.Orcamentos.ToListAsync();
        orcamentos.Should().HaveCount(2);
        orcamentos.Should().Contain(o => o.Id == orcamento1.Id);
        orcamentos.Should().Contain(o => o.Id == orcamento2.Id);
    }

    #endregion

    #region GetBudgetByOsIdAsync Tests

    [Fact]
    public async Task GetBudgetByOsIdAsync_WithExistingOsId_ShouldReturnOrcamento()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamento = DomainEntityBuilder.CreateOrcamento(osId);
        var context = _dbFixture.GetContext();
        context.Orcamentos.Add(orcamento);
        await context.SaveChangesAsync();

        var service = new OrcamentoService(context);

        // Act
        var result = await service.GetBudgetByOsIdAsync(osId);

        // Assert
        result.Should().NotBeNull();
        result!.OsId.Should().Be(osId);
    }

    [Fact]
    public async Task GetBudgetByOsIdAsync_WithNonExistentOsId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentOsId = Guid.NewGuid();

        // Act
        var result = await _service.GetBudgetByOsIdAsync(nonExistentOsId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBudgetByOsIdAsync_WithMultipleOrcamentos_ShouldReturnCorrectOne()
    {
        // Arrange
        var osId1 = Guid.NewGuid();
        var osId2 = Guid.NewGuid();
        var orcamento1 = DomainEntityBuilder.CreateOrcamento(osId1, valor: 100.00m);
        var orcamento2 = DomainEntityBuilder.CreateOrcamento(osId2, valor: 200.00m);

        var context = _dbFixture.GetContext();
        context.Orcamentos.AddRange(orcamento1, orcamento2);
        await context.SaveChangesAsync();

        var service = new OrcamentoService(context);

        // Act
        var result = await service.GetBudgetByOsIdAsync(osId1);

        // Assert
        result.Should().NotBeNull();
        result!.Valor.Should().Be(100.00m);
        result.OsId.Should().Be(osId1);
    }

    #endregion

    #region ObterOrcamentoPorOsIdAsync Tests

    [Fact]
    public async Task ObterOrcamentoPorOsIdAsync_ShouldCallGetBudgetByOsIdAsync()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamento = DomainEntityBuilder.CreateOrcamento(osId);
        var context = _dbFixture.GetContext();
        context.Orcamentos.Add(orcamento);
        await context.SaveChangesAsync();

        var service = new OrcamentoService(context);

        // Act
        var result = await service.ObterOrcamentoPorOsIdAsync(osId);

        // Assert
        result.Should().NotBeNull();
        result!.OsId.Should().Be(osId);
    }

    #endregion

    #region AprovaBudgetAsync Tests

    [Fact]
    public async Task AprovaBudgetAsync_WithValidOrcamento_ShouldUpdateStatus()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamento = DomainEntityBuilder.CreateOrcamento(
            osId, status: StatusOrcamento.Enviado);

        var context = _dbFixture.GetContext();
        context.Orcamentos.Add(orcamento);
        await context.SaveChangesAsync();

        var service = new OrcamentoService(context);

        // Act
        var result = await service.AprovaBudgetAsync(osId);

        // Assert
        result.Status.Should().Be(StatusOrcamento.Aprovado);
    }

    [Fact]
    public async Task AprovaBudgetAsync_WithNonExistentOrcamento_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var nonExistentOsId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.AprovaBudgetAsync(nonExistentOsId));
    }

    [Fact]
    public async Task AprovaBudgetAsync_WithApprovedOrcamento_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamento = DomainEntityBuilder.CreateOrcamento(
            osId, status: StatusOrcamento.Aprovado);

        var context = _dbFixture.GetContext();
        context.Orcamentos.Add(orcamento);
        await context.SaveChangesAsync();

        var service = new OrcamentoService(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AprovaBudgetAsync(osId));
    }

    [Fact]
    public async Task AprovaBudgetAsync_WithRejectedOrcamento_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamento = DomainEntityBuilder.CreateOrcamento(
            osId, status: StatusOrcamento.Rejeitado);

        var context = _dbFixture.GetContext();
        context.Orcamentos.Add(orcamento);
        await context.SaveChangesAsync();

        var service = new OrcamentoService(context);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AprovaBudgetAsync(osId));
    }

    [Fact]
    public async Task AprovaBudgetAsync_WithCustomCorrelationId_ShouldPropagateCorrelationId()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var customCorrelationId = Guid.NewGuid();
        var orcamento = DomainEntityBuilder.CreateOrcamento(osId);

        var context = _dbFixture.GetContext();
        context.Orcamentos.Add(orcamento);
        await context.SaveChangesAsync();

        var service = new OrcamentoService(context);

        // Act
        var result = await service.AprovaBudgetAsync(osId, customCorrelationId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(StatusOrcamento.Aprovado);
    }

    #endregion
}
