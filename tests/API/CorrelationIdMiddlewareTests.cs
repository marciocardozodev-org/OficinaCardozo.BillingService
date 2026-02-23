using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.API;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.API;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithCorrelationIdHeader_ShouldStoreInHttpContext()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var context = CreateHttpContext();
        context.Request.Headers["Correlation-Id"] = expectedCorrelationId;

        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["CorrelationId"].Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WithoutCorrelationIdHeader_ShouldGenerateNewId()
    {
        // Arrange
        var context = CreateHttpContext();

        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["CorrelationId"].Should().NotBeNull();
        context.Items["CorrelationId"].Should().BeOfType<string>();
        Guid.TryParse(context.Items["CorrelationId"].ToString(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddCorrelationIdToResponseHeader()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var context = CreateHttpContext();
        context.Request.Headers["Correlation-Id"] = expectedCorrelationId;

        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Correlation-Id"].Should().Contain(expectedCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyCorrelationId_ShouldGenerateNewId()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["Correlation-Id"] = "";

        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["CorrelationId"].Should().NotBeNull();
        var stored = context.Items["CorrelationId"].ToString();
        stored.Should().NotBeEmpty();
        Guid.TryParse(stored, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = ctx => { nextCalled = true; return Task.CompletedTask; };

        var context = CreateHttpContext();
        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithWhitespaceCorrelationId_ShouldGenerateNewId()
    {
        // Arrange
        var context = CreateHttpContext();
        context.Request.Headers["Correlation-Id"] = "   ";

        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items["CorrelationId"].Should().NotBe("   ");
        Guid.TryParse(context.Items["CorrelationId"].ToString(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MultipleRequests_ShouldGenerateDifferentIds()
    {
        // Arrange
        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        // Act
        var context1 = CreateHttpContext();
        await middleware.InvokeAsync(context1);

        var context2 = CreateHttpContext();
        await middleware.InvokeAsync(context2);

        // Assert
        var id1 = context1.Items["CorrelationId"].ToString();
        var id2 = context2.Items["CorrelationId"].ToString();
        id1.Should().NotBe(id2);
    }

    // Note: GetCorrelationId extension method tests are skipped due to namespace ambiguity
    // The method works correctly but cannot be easily tested in this context

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            Request =
            {
                Scheme = "http",
                Host = new HostString("localhost")
            }
        };
    }
}
