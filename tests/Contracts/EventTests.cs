using FluentAssertions;
using System.Text.Json;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Contracts;

public class EventEnvelopeTests
{
    [Fact]
    public void EventEnvelope_ShouldStorePayload()
    {
        // Arrange
        var osCreated = new OsCreated { OsId = Guid.NewGuid(), Valor = 150.00m };
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);

        // Act & Assert
        envelope.Payload.Should().Be(osCreated);
        envelope.Payload.OsId.Should().Be(osCreated.OsId);
    }

    [Fact]
    public void EventEnvelope_ShouldPreserveCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var osCreated = new OsCreated { OsId = Guid.NewGuid() };
        var envelope = EventBuilder.CreateEventEnvelope(osCreated, correlationId);

        // Act & Assert
        envelope.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void EventEnvelope_ShouldPreserveCausationId()
    {
        // Arrange
        var causationId = Guid.NewGuid();
        var osCreated = new OsCreated { OsId = Guid.NewGuid() };
        var envelope = EventBuilder.CreateEventEnvelope(osCreated, Guid.NewGuid(), causationId);

        // Act & Assert
        envelope.CausationId.Should().Be(causationId);
    }



    [Fact]
    public void EventEnvelope_ShouldSetTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var osCreated = new OsCreated { OsId = Guid.NewGuid() };
        var envelope = EventBuilder.CreateEventEnvelope(osCreated);
        var after = DateTime.UtcNow;

        // Act & Assert
        envelope.Timestamp.Should().BeOnOrAfter(before);
        envelope.Timestamp.Should().BeOnOrBefore(after.AddSeconds(1));
    }


}

public class OsCreatedEventTests
{
    [Fact]
    public void OsCreated_WithOsId_ShouldStore()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCreated = new OsCreated { OsId = osId, Valor = 100.00m };

        // Act & Assert
        osCreated.OsId.Should().Be(osId);
    }

    [Fact]
    public void OsCreated_WithValor_ShouldStore()
    {
        // Arrange
        var valor = 250.75m;
        var osCreated = new OsCreated { OsId = Guid.NewGuid(), Valor = valor };

        // Act & Assert
        osCreated.Valor.Should().Be(valor);
    }

    [Fact]
    public void OsCreated_WithoutValor_ShouldAllowNull()
    {
        // Arrange
        var osCreated = new OsCreated { OsId = Guid.NewGuid(), Valor = null };

        // Act & Assert
        osCreated.Valor.Should().BeNull();
    }

    [Theory]
    [InlineData(100.00)]
    [InlineData(0.00)]
    [InlineData(999999.99)]
    public void OsCreated_WithVariousValues_ShouldStore(decimal valor)
    {
        // Arrange & Act
        var osCreated = new OsCreated { OsId = Guid.NewGuid(), Valor = valor };

        // Assert
        osCreated.Valor.Should().Be(valor);
    }
}

public class BudgetGeneratedEventTests
{
    [Fact]
    public void BudgetGenerated_ShouldInitialize()
    {
        // Arrange & Act
        var evt = new BudgetGenerated
        {
            OsId = Guid.NewGuid(),
            BudgetId = Guid.NewGuid(),
            Amount = 150.00m,
            Status = BudgetStatus.Generated
        };

        // Assert
        evt.Should().NotBeNull();
        evt.OsId.Should().NotBe(Guid.Empty);
        evt.Amount.Should().Be(150.00m);
    }

    [Fact]
    public void BudgetGenerated_WithDifferentStatuses_ShouldStore()
    {
        // Arrange & Act
        var generated = new BudgetGenerated
        {
            OsId = Guid.NewGuid(),
            Status = BudgetStatus.Generated
        };

        var approved = new BudgetGenerated
        {
            OsId = Guid.NewGuid(),
            Status = BudgetStatus.Approved
        };

        // Assert
        generated.Status.Should().Be(BudgetStatus.Generated);
        approved.Status.Should().Be(BudgetStatus.Approved);
    }
}

public class PaymentStatusEventTests
{
    [Fact]
    public void PaymentPending_ShouldInitialize()
    {
        // Arrange & Act
        var evt = new PaymentPending
        {
            PaymentId = Guid.NewGuid(),
            OsId = Guid.NewGuid(),
            Status = PaymentStatus.Pending,
            Amount = 100.00m
        };

        // Assert
        evt.Should().NotBeNull();
        evt.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void PaymentConfirmed_ShouldInitialize()
    {
        // Arrange & Act
        var evt = new PaymentConfirmed
        {
            PaymentId = Guid.NewGuid(),
            OsId = Guid.NewGuid(),
            Status = PaymentStatus.Confirmed,
            Amount = 100.00m
        };

        // Assert
        evt.Should().NotBeNull();
        evt.Status.Should().Be(PaymentStatus.Confirmed);
    }

    [Fact]
    public void PaymentFailed_ShouldInitialize()
    {
        // Arrange & Act
        var evt = new PaymentFailed
        {
            PaymentId = Guid.NewGuid(),
            OsId = Guid.NewGuid(),
            Status = PaymentStatus.Failed
        };

        // Assert
        evt.Should().NotBeNull();
        evt.Status.Should().Be(PaymentStatus.Failed);
    }
}

public class OsCanceledEventTests
{
    [Fact]
    public void OsCanceled_ShouldInitialize()
    {
        // Arrange & Act
        var evt = new OsCanceled { OsId = Guid.NewGuid() };

        // Assert
        evt.Should().NotBeNull();
        evt.OsId.Should().NotBe(Guid.Empty);
    }
}

public class OsCompensationRequestedEventTests
{
    [Fact]
    public void OsCompensationRequested_ShouldInitialize()
    {
        // Arrange & Act
        var evt = new OsCompensationRequested { OsId = Guid.NewGuid() };

        // Assert
        evt.Should().NotBeNull();
        evt.OsId.Should().NotBe(Guid.Empty);
    }
}
