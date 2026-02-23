using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Handlers;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Handlers;

public class OsCanceledHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidEnvelope_ShouldCompleteWithoutError()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var osCanceled = new OsCanceled { OsId = osId };
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        var envelope = new EventEnvelope<OsCanceled>
        {
            Payload = osCanceled,
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = DateTime.UtcNow
        };

        var handler = new OsCanceledHandler();

        // Act
        var action = async () => await handler.HandleAsync(envelope);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_WithNullOsId_ShouldCompleteWithoutError()
    {
        // Arrange
        var osCanceled = new OsCanceled { OsId = Guid.Empty };
        var envelope = EventBuilder.CreateEventEnvelope(osCanceled);

        var handler = new OsCanceledHandler();

        // Act
        var action = async () => await handler.HandleAsync(envelope);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_MultipleEnvelopes_ShouldHandleAll()
    {
        // Arrange
        var handler = new OsCanceledHandler();
        var envelopes = new List<EventEnvelope<OsCanceled>>
        {
            EventBuilder.CreateEventEnvelope(new OsCanceled { OsId = Guid.NewGuid() }),
            EventBuilder.CreateEventEnvelope(new OsCanceled { OsId = Guid.NewGuid() }),
            EventBuilder.CreateEventEnvelope(new OsCanceled { OsId = Guid.NewGuid() })
        };

        // Act
        var action = async () =>
        {
            foreach (var envelope in envelopes)
            {
                await handler.HandleAsync(envelope);
            }
        };

        // Assert
        await action.Should().NotThrowAsync();
    }
}

public class OsCompensationRequestedHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidEnvelope_ShouldCompleteWithoutError()
    {
        // Arrange
        var osId = Guid.NewGuid();
        var compensationRequested = new OsCompensationRequested { OsId = osId };
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        var envelope = new EventEnvelope<OsCompensationRequested>
        {
            Payload = compensationRequested,
            CorrelationId = correlationId,
            CausationId = causationId,
            Timestamp = DateTime.UtcNow
        };

        var handler = new OsCompensationRequestedHandler();

        // Act
        var action = async () => await handler.HandleAsync(envelope);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_WithNullOsId_ShouldCompleteWithoutError()
    {
        // Arrange
        var compensationRequested = new OsCompensationRequested { OsId = Guid.Empty };
        var envelope = EventBuilder.CreateEventEnvelope(compensationRequested);

        var handler = new OsCompensationRequestedHandler();

        // Act
        var action = async () => await handler.HandleAsync(envelope);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_MultipleEnvelopes_ShouldHandleAll()
    {
        // Arrange
        var handler = new OsCompensationRequestedHandler();
        var envelopes = new List<EventEnvelope<OsCompensationRequested>>
        {
            EventBuilder.CreateEventEnvelope(new OsCompensationRequested { OsId = Guid.NewGuid() }),
            EventBuilder.CreateEventEnvelope(new OsCompensationRequested { OsId = Guid.NewGuid() }),
            EventBuilder.CreateEventEnvelope(new OsCompensationRequested { OsId = Guid.NewGuid() })
        };

        // Act
        var action = async () =>
        {
            foreach (var envelope in envelopes)
            {
                await handler.HandleAsync(envelope);
            }
        };

        // Assert
        await action.Should().NotThrowAsync();
    }
}
