using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PayFlow.Domain.Entities;
using PayFlow.Infrastructure.Middleware;
using PayFlow.Infrastructure.Persistence;
using Xunit;

namespace PayFlow.UnitTests;

public class ApiKeyAuthenticationMiddlewareTests
{
    private readonly PayFlowDbContext _dbContext;
    private readonly Guid _merchantId = Guid.NewGuid();
    private const string ValidApiKey = "pk_live_valid_key_123";
    private const string ValidApiSecret = "sk_live_valid_secret_456";

    public ApiKeyAuthenticationMiddlewareTests()
    {
        var options = new DbContextOptionsBuilder<PayFlowDbContext>()
            .UseInMemoryDatabase(databaseName: $"ApiKeyAuthTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PayFlowDbContext(options);

        // Active merchant
        _dbContext.Merchants.Add(new Merchant
        {
            Id = _merchantId,
            Name = "Active Merchant Corp",
            ApiKey = ValidApiKey,
            ApiSecret = ValidApiSecret,
            IsActive = true
        });

        // Inactive merchant
        _dbContext.Merchants.Add(new Merchant
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Merchant LLC",
            ApiKey = "pk_inactive_merchant",
            ApiSecret = "sk_inactive_merchant",
            IsActive = false
        });

        _dbContext.SaveChanges();
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger")]
    [InlineData("/health")]
    [InlineData("/api/v1/webhooks/Opn")]
    [InlineData("/api/v1/webhooks/GBPrimePay")]
    public async Task InvokeAsync_WhenPublicEndpoint_BypassesAuthentication(string publicPath)
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ApiKeyAuthenticationMiddleware(next);

        var context = new DefaultHttpContext();
        context.Request.Path = publicPath;

        // Act
        await middleware.InvokeAsync(context, _dbContext);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidXApiKeyHeaderProvided_AuthenticatesAndCallsNext()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ApiKeyAuthenticationMiddleware(next);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/payments";
        context.Request.Headers["X-API-Key"] = ValidApiKey;

        // Act
        await middleware.InvokeAsync(context, _dbContext);

        // Assert
        nextCalled.Should().BeTrue();
        context.Items["MerchantId"].Should().Be(_merchantId);
        context.User.Identity?.IsAuthenticated.Should().BeTrue();
        context.User.FindFirst("ApiKey")?.Value.Should().Be(ValidApiKey);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidAuthorizationBearerProvided_AuthenticatesAndCallsNext()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ApiKeyAuthenticationMiddleware(next);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/payments";
        context.Request.Headers["Authorization"] = $"Bearer {ValidApiKey}";

        // Act
        await middleware.InvokeAsync(context, _dbContext);

        // Assert
        nextCalled.Should().BeTrue();
        context.Items["MerchantId"].Should().Be(_merchantId);
    }

    [Fact]
    public async Task InvokeAsync_WhenApiKeyMissing_Returns401Unauthorized()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ApiKeyAuthenticationMiddleware(next);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/payments";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context, _dbContext);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WhenApiKeyIsInvalid_Returns401Unauthorized()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ApiKeyAuthenticationMiddleware(next);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/payments";
        context.Request.Headers["X-API-Key"] = "pk_invalid_bogus_token";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context, _dbContext);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WhenMerchantIsInactive_Returns401Unauthorized()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ApiKeyAuthenticationMiddleware(next);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/payments";
        context.Request.Headers["X-API-Key"] = "pk_inactive_merchant";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context, _dbContext);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
