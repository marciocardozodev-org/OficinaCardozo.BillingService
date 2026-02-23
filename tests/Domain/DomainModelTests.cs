using FluentAssertions;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.Domain;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Domain;

public class PagamentoTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange & Act
        var pagamento = DomainEntityBuilder.CreatePagamento();

        // Assert
        pagamento.Should().NotBeNull();
        pagamento.OsId.Should().NotBe(Guid.Empty);
        pagamento.OrcamentoId.Should().Be(1L);
        pagamento.Valor.Should().Be(100.00m);
        pagamento.Status.Should().Be(StatusPagamento.Pendente);
    }

    [Fact]
    public void Pagamento_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var orcamentoId = 42L;
        var valor = 250.50m;

        // Act
        var pagamento = DomainEntityBuilder.CreatePagamento(osId, orcamentoId, valor);

        // Assert
        pagamento.OsId.Should().Be(osId);
        pagamento.OrcamentoId.Should().Be(orcamentoId);
        pagamento.Valor.Should().Be(valor);
    }

    [Fact]
    public void Pagamento_StatusEnum_ShouldHaveAllValues()
    {
        // Arrange
        var statuses = Enum.GetValues(typeof(StatusPagamento)).Cast<StatusPagamento>();

        // Act & Assert
        statuses.Should().Contain(StatusPagamento.Pendente);
        statuses.Should().Contain(StatusPagamento.Confirmado);
        statuses.Should().Contain(StatusPagamento.Falhou);
    }

    [Fact]
    public void Pagamento_WithProviderPaymentId_ShouldStore()
    {
        // Arrange
        var pagamento = DomainEntityBuilder.CreatePagamento();
        var providerId = "mp_payment_123456";

        // Act
        pagamento.ProviderPaymentId = providerId;

        // Assert
        pagamento.ProviderPaymentId.Should().Be(providerId);
    }

    [Fact]
    public void Pagamento_WithDifferentMethods_ShouldStore()
    {
        // Arrange
        var methods = new[] { "PIX", "CREDIT_CARD", "BOLETO", "TRANSFER" };

        // Act & Assert
        foreach (var method in methods)
        {
            var pagamento = DomainEntityBuilder.CreatePagamento(metodo: method);
            pagamento.Metodo.Should().Be(method);
        }
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(1.00)]
    [InlineData(99999.99)]
    [InlineData(0.01)]
    public void Pagamento_WithVariousValues_ShouldStore(decimal valor)
    {
        // Arrange & Act
        var pagamento = DomainEntityBuilder.CreatePagamento(valor: valor);

        // Assert
        pagamento.Valor.Should().Be(valor);
    }
}

public class OrcamentoTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange & Act
        var orcamento = DomainEntityBuilder.CreateOrcamento();

        // Assert
        orcamento.Should().NotBeNull();
        orcamento.OsId.Should().NotBe(Guid.Empty);
        orcamento.Valor.Should().Be(100.00m);
        orcamento.Status.Should().Be(StatusOrcamento.Enviado);
    }

    [Fact]
    public void Orcamento_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var valor = 350.75m;
        var email = "client@example.com";

        // Act
        var orcamento = DomainEntityBuilder.CreateOrcamento(osId, valor, email);

        // Assert
        orcamento.OsId.Should().Be(osId);
        orcamento.Valor.Should().Be(valor);
        orcamento.EmailCliente.Should().Be(email);
    }

    [Fact]
    public void Orcamento_StatusEnum_ShouldHaveAllValues()
    {
        // Arrange
        var statuses = Enum.GetValues(typeof(StatusOrcamento)).Cast<StatusOrcamento>();

        // Act & Assert
        statuses.Should().Contain(StatusOrcamento.Pendente);
        statuses.Should().Contain(StatusOrcamento.Enviado);
        statuses.Should().Contain(StatusOrcamento.Aprovado);
        statuses.Should().Contain(StatusOrcamento.Rejeitado);
    }

    [Fact]
    public void Orcamento_WithDifferentStatuses_ShouldStore()
    {
        // Arrange & Act
        var pendente = DomainEntityBuilder.CreateOrcamento(status: StatusOrcamento.Pendente);
        var enviado = DomainEntityBuilder.CreateOrcamento(status: StatusOrcamento.Enviado);
        var aprovado = DomainEntityBuilder.CreateOrcamento(status: StatusOrcamento.Aprovado);
        var rejeitado = DomainEntityBuilder.CreateOrcamento(status: StatusOrcamento.Rejeitado);

        // Assert
        pendente.Status.Should().Be(StatusOrcamento.Pendente);
        enviado.Status.Should().Be(StatusOrcamento.Enviado);
        aprovado.Status.Should().Be(StatusOrcamento.Aprovado);
        rejeitado.Status.Should().Be(StatusOrcamento.Rejeitado);
    }

    [Fact]
    public void Orcamento_WithVariousEmails_ShouldStore()
    {
        // Arrange
        var emails = new[]
        {
            "test@example.com",
            "user+tag@domain.co.uk",
            "special.chars_123@test.br",
            ""
        };

        // Act & Assert
        foreach (var email in emails)
        {
            var orcamento = DomainEntityBuilder.CreateOrcamento(emailCliente: email);
            orcamento.EmailCliente.Should().Be(email);
        }
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(50.00)]
    [InlineData(1000.00)]
    public void Orcamento_WithVariousValues_ShouldStore(decimal valor)
    {
        // Arrange & Act
        var orcamento = DomainEntityBuilder.CreateOrcamento(valor: valor);

        // Assert
        orcamento.Valor.Should().Be(valor);
    }

    [Fact]
    public void Orcamento_CorrelationAndCausationIds_ShouldBeUnique()
    {
        // Arrange & Act
        var orcamento1 = DomainEntityBuilder.CreateOrcamento();
        var orcamento2 = DomainEntityBuilder.CreateOrcamento();

        // Assert
        orcamento1.CorrelationId.Should().NotBe(orcamento2.CorrelationId);
        orcamento1.CausationId.Should().NotBe(orcamento2.CausationId);
    }
}


public class BudgetApprovedTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var budgetApproved = new BudgetApproved
        {
            OrcamentoId = 1L,
            OsId = Guid.NewGuid(),
            Valor = 100.00m,
            Status = "Aprovado",
            ApprovedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };

        // Assert
        budgetApproved.Should().NotBeNull();
        budgetApproved.OrcamentoId.Should().Be(1L);
        budgetApproved.Valor.Should().Be(100.00m);
    }
}
