using FluentAssertions;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.API.Billing;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.API;

public class IdempotencyServiceTests
{
    private readonly IdempotencyService _service;

    public IdempotencyServiceTests()
    {
        _service = new IdempotencyService();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithValidEventId_ShouldReturnFalse()
    {
        // Arrange
        var eventId = "event_123";

        // Act
        var result = await _service.IsDuplicateAsync(eventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithAnyEventId_ShouldReturnFalse()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();

        // Act
        var result = await _service.IsDuplicateAsync(eventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithEmptyEventId_ShouldReturnFalse()
    {
        // Arrange
        var eventId = "";

        // Act
        var result = await _service.IsDuplicateAsync(eventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDuplicateAsync_WithNullEventId_ShouldHandleGracefully()
    {
        // Arrange
        string? eventId = null;

        // Act
        try
        {
            var result = await _service.IsDuplicateAsync(eventId!);
            result.Should().BeFalse();
        }
        catch (ArgumentNullException)
        {
            // Expected behavior if null is not allowed
        }
    }
}

public class WebhookValidatorTests
{
    private readonly WebhookValidator _service;

    public WebhookValidatorTests()
    {
        _service = new WebhookValidator();
    }

    [Fact]
    public async Task IsValidAsync_WithValidEventId_ShouldReturnTrue()
    {
        // Arrange
        var eventId = "webhook_123";

        // Act
        var result = await _service.IsValidAsync(eventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_WithUuidEventId_ShouldReturnTrue()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();

        // Act
        var result = await _service.IsValidAsync(eventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_WithEmptyEventId_ShouldReturnTrue()
    {
        // Arrange
        var eventId = "";

        // Act
        var result = await _service.IsValidAsync(eventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_WithLongEventId_ShouldReturnTrue()
    {
        // Arrange
        var eventId = new string('a', 1000);

        // Act
        var result = await _service.IsValidAsync(eventId);

        // Assert
        result.Should().BeTrue();
    }
}
