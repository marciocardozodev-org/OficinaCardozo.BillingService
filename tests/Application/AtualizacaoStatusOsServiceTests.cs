using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.Application;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Application;

public class AtualizacaoStatusOsServiceTests : IDisposable
{
    private readonly BillingDbContextFixture _dbFixture;
    private readonly AtualizacaoStatusOsService _service;

    public AtualizacaoStatusOsServiceTests()
    {
        _dbFixture = new BillingDbContextFixture();
        _service = new AtualizacaoStatusOsService(_dbFixture.GetContext());
    }

    public void Dispose()
    {
        _dbFixture.Dispose();
    }

    #region AtualizarStatus Tests

    [Fact]
    public void AtualizarStatus_WithValidData_ShouldCreateAtualizacao()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var novoStatus = "Iniciada";

        // Act
        var result = _service.AtualizarStatus(osId, novoStatus);

        // Assert
        result.Should().NotBeNull();
        result.OsId.Should().Be(osId);
        result.NovoStatus.Should().Be(novoStatus);
    }

    [Fact]
    public void AtualizarStatus_WithEventType_ShouldStore()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var novoStatus = "Concluída";
        var eventType = "OsCompleted";

        // Act
        var result = _service.AtualizarStatus(osId, novoStatus, eventType);

        // Assert
        result.EventType.Should().Be(eventType);
    }

    [Fact]
    public void AtualizarStatus_WithCorrelationId_ShouldStore()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        // Act
        var result = _service.AtualizarStatus(
            osId, "Iniciada", null, correlationId);

        // Assert
        result.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void AtualizarStatus_WithCausationId_ShouldStore()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        // Act
        var result = _service.AtualizarStatus(
            osId, "Iniciada", null, null, causationId);

        // Assert
        result.CausationId.Should().Be(causationId);
    }

    [Fact]
    public void AtualizarStatus_WithAllParameters_ShouldStore()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var novoStatus = "Cancelada";
        var eventType = "OsCanceled";
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        // Act
        var result = _service.AtualizarStatus(
            osId, novoStatus, eventType, correlationId, causationId);

        // Assert
        result.OsId.Should().Be(osId);
        result.NovoStatus.Should().Be(novoStatus);
        result.EventType.Should().Be(eventType);
        result.CorrelationId.Should().Be(correlationId);
        result.CausationId.Should().Be(causationId);
    }

    [Fact]
    public void AtualizarStatus_ShouldPersistToDatabase()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var novoStatus = "Iniciada";

        // Act
        var result = _service.AtualizarStatus(osId, novoStatus);

        // Assert
        var context = _dbFixture.GetContext();
        var saved = context.AtualizacoesStatusOs
            .FirstOrDefault(a => a.OsId == osId);

        saved.Should().NotBeNull();
        saved!.NovoStatus.Should().Be(novoStatus);
    }

    [Fact]
    public void AtualizarStatus_MultipleUpdates_ShouldPersistAll()
    {
        // Arrange
        var osId = Guid.NewGuid();

        // Act
        _service.AtualizarStatus(osId, "Pendente");
        _service.AtualizarStatus(osId, "Iniciada");
        _service.AtualizarStatus(osId, "Concluída");

        // Assert
        var context = _dbFixture.GetContext();
        var updates = context.AtualizacoesStatusOs
            .Where(a => a.OsId == osId)
            .ToList();

        updates.Should().HaveCount(3);
    }

    [Fact]
    public void AtualizarStatus_ShouldSetTimestamp()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var result = _service.AtualizarStatus(osId, "Iniciada");

        // Assert
        result.AtualizadoEm.Should().BeOnOrAfter(before);
        result.AtualizadoEm.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }

    #endregion

    #region ListarPorOrdem Tests

    [Fact]
    public void ListarPorOrdem_WithExistingUpdates_ShouldReturnAll()
    {
        // Arrange
        var osId = Guid.NewGuid();
        _service.AtualizarStatus(osId, "Pendente");
        _service.AtualizarStatus(osId, "Iniciada");
        _service.AtualizarStatus(osId, "Concluída");

        // Act
        var result = _service.ListarPorOrdem(osId);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(a => a.OsId.Should().Be(osId));
    }

    [Fact]
    public void ListarPorOrdem_WithNoUpdates_ShouldReturnEmpty()
    {
        // Arrange
        var osId = Guid.NewGuid();

        // Act
        var result = _service.ListarPorOrdem(osId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ListarPorOrdem_ShouldNotReturnOtherOsUpdates()
    {
        // Arrange
        var osId1 = Guid.NewGuid();
        var osId2 = Guid.NewGuid();

        _service.AtualizarStatus(osId1, "Pendente");
        _service.AtualizarStatus(osId1, "Iniciada");
        _service.AtualizarStatus(osId2, "Pendente");

        // Act
        var result = _service.ListarPorOrdem(osId1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.OsId.Should().Be(osId1));
    }

    [Fact]
    public void ListarPorOrdem_ShouldReturnInChronologicalOrder()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var statuses = new[] { "Pendente", "Iniciada", "Concluída" };

        foreach (var status in statuses)
        {
            _service.AtualizarStatus(osId, status);
        }

        // Act
        var result = _service.ListarPorOrdem(osId).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].NovoStatus.Should().Be("Pendente");
        result[1].NovoStatus.Should().Be("Iniciada");
        result[2].NovoStatus.Should().Be("Concluída");
    }

    [Fact]
    public void ListarPorOrdem_WithVariousStatuses_ShouldReturnAll()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var statusUpdates = new[]
        {
            "Criada",
            "Orçamento Enviado",
            "Orçamento Aprovado",
            "Pagamento Iniciado",
            "Pagamento Confirmado",
            "Iniciada",
            "Concluída"
        };

        foreach (var status in statusUpdates)
        {
            _service.AtualizarStatus(osId, status);
        }

        // Act
        var result = _service.ListarPorOrdem(osId).ToList();

        // Assert
        result.Should().HaveCount(7);
        for (int i = 0; i < statusUpdates.Length; i++)
        {
            result[i].NovoStatus.Should().Be(statusUpdates[i]);
        }
    }

    #endregion
}
