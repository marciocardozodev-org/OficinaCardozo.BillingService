using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using OFICINACARDOZO.BILLINGSERVICE.Tests.Fixtures;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.Messaging;

public class OutboxProcessingTests : IDisposable
{
    private readonly BillingDbContextFixture _dbFixture;

    public OutboxProcessingTests()
    {
        _dbFixture = new BillingDbContextFixture();
    }

    public void Dispose()
    {
        _dbFixture.Dispose();
    }

    #region OutboxMessage Tests

    [Fact]
    public async Task OutboxMessage_ShouldBePersisted()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var message = new OutboxMessage
        {
            AggregateId = Guid.NewGuid(),
            AggregateType = "OrderService",
            EventType = nameof(BudgetGenerated),
            Payload = "{\"test\": \"data\"}",
            CreatedAt = DateTime.UtcNow,
            Published = false,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };

        // Act
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Assert
        var savedMessage = await context.OutboxMessages.FirstOrDefaultAsync();
        savedMessage.Should().NotBeNull();
        savedMessage!.EventType.Should().Be(nameof(BudgetGenerated));
        savedMessage.Published.Should().BeFalse();
    }

    [Fact]
    public async Task OutboxMessage_ShouldMarkAsPublished()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var message = new OutboxMessage
        {
            AggregateId = Guid.NewGuid(),
            AggregateType = "OrderService",
            EventType = nameof(PaymentConfirmed),
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Published = false,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };

        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Act
        await context.OutboxMessages
            .Where(m => m.Id == message.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Published, true)
                .SetProperty(m => m.PublishedAt, DateTime.UtcNow));

        // Assert
        var updatedMessage = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == message.Id);
        updatedMessage!.Published.Should().BeTrue();
        updatedMessage.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OutboxMessage_MultipleMessages_ShouldStoreAll()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var messages = new List<OutboxMessage>
        {
            CreateOutboxMessage(nameof(BudgetGenerated)),
            CreateOutboxMessage(nameof(PaymentConfirmed)),
            CreateOutboxMessage(nameof(PaymentFailed))
        };

        // Act
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync();

        // Assert
        var allMessages = await context.OutboxMessages.ToListAsync();
        allMessages.Should().HaveCount(3);
    }

    [Fact]
    public async Task OutboxMessage_QueryUnpublished_ShouldReturnOnlyUnpublished()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var message1 = CreateOutboxMessage(nameof(BudgetGenerated), published: false);
        var message2 = CreateOutboxMessage(nameof(PaymentConfirmed), published: true);
        var message3 = CreateOutboxMessage(nameof(PaymentFailed), published: false);

        context.OutboxMessages.AddRange(message1, message2, message3);
        await context.SaveChangesAsync();

        // Act
        var unpublished = await context.OutboxMessages
            .Where(m => !m.Published)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Assert
        unpublished.Should().HaveCount(2);
        unpublished.Should().NotContain(m => m.Id == message2.Id);
    }

    [Fact]
    public async Task OutboxMessage_WithCorrelationId_ShouldPreserveValue()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var correlationId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            AggregateId = Guid.NewGuid(),
            AggregateType = "OrderService",
            EventType = nameof(BudgetGenerated),
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Published = false,
            CorrelationId = correlationId,
            CausationId = Guid.NewGuid()
        };

        // Act
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.OutboxMessages.FirstOrDefaultAsync();
        saved!.CorrelationId.Should().Be(correlationId);
    }

    #endregion

    #region InboxMessage Tests

    [Fact]
    public async Task InboxMessage_ShouldBePersisted()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var message = new InboxMessage
        {
            ProviderEventId = "provider_123",
            EventType = nameof(OsCreated),
            Payload = "{\"osId\": \"test\"}",
            ReceivedAt = DateTime.UtcNow,
            Processed = false,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };

        // Act
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Assert
        var saved = await context.InboxMessages.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.ProviderEventId.Should().Be("provider_123");
        saved.Processed.Should().BeFalse();
    }

    [Fact]
    public async Task InboxMessage_ShouldMarkAsProcessed()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var message = new InboxMessage
        {
            ProviderEventId = "provider_456",
            EventType = nameof(OsCreated),
            Payload = "{}",
            ReceivedAt = DateTime.UtcNow,
            Processed = false,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };

        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Act
        await context.InboxMessages
            .Where(m => m.Id == message.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Processed, true)
                .SetProperty(m => m.ProcessedAt, DateTime.UtcNow));

        // Assert
        var updated = await context.InboxMessages.FirstOrDefaultAsync(m => m.Id == message.Id);
        updated!.Processed.Should().BeTrue();
        updated.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task InboxMessage_PreventsDuplicate_WithProviderEventId()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var providerEventId = "unique_provider_event_123";
        var message = new InboxMessage
        {
            ProviderEventId = providerEventId,
            EventType = nameof(OsCreated),
            Payload = "{}",
            ReceivedAt = DateTime.UtcNow,
            Processed = false,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };

        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Act
        var existing = await context.InboxMessages
            .FirstOrDefaultAsync(m => m.ProviderEventId == providerEventId);

        // Assert
        existing.Should().NotBeNull();
        existing!.Id.Should().Be(message.Id);
    }

    [Fact]
    public async Task InboxMessage_MultipleMessages_ShouldStoreAll()
    {
        // Arrange
        var context = _dbFixture.GetContext();
        var messages = new List<InboxMessage>
        {
            CreateInboxMessage("provider_1", nameof(OsCreated)),
            CreateInboxMessage("provider_2", nameof(OsCreated)),
            CreateInboxMessage("provider_3", nameof(OsCanceled))
        };

        // Act
        context.InboxMessages.AddRange(messages);
        await context.SaveChangesAsync();

        // Assert
        var all = await context.InboxMessages.ToListAsync();
        all.Should().HaveCount(3);
    }

    #endregion

    #region Helpers

    private OutboxMessage CreateOutboxMessage(
        string eventType = nameof(BudgetGenerated),
        bool published = false)
    {
        return new OutboxMessage
        {
            AggregateId = Guid.NewGuid(),
            AggregateType = "OrderService",
            EventType = eventType,
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Published = published,
            PublishedAt = published ? DateTime.UtcNow : null,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };
    }

    private InboxMessage CreateInboxMessage(
        string providerEventId,
        string eventType = nameof(OsCreated))
    {
        return new InboxMessage
        {
            ProviderEventId = providerEventId,
            EventType = eventType,
            Payload = "{}",
            ReceivedAt = DateTime.UtcNow,
            Processed = false,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid()
        };
    }

    #endregion
}
