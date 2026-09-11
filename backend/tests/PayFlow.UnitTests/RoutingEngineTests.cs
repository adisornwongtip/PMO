using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.DTOs;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Services;
using Xunit;

namespace PayFlow.UnitTests;

public class RoutingEngineTests
{
    private readonly PayFlowDbContext _dbContext;
    private readonly ProviderHealthTracker _healthTracker;
    private readonly RoutingEngine _routingEngine;
    private readonly Guid _merchantId = Guid.NewGuid();

    public RoutingEngineTests()
    {
        var options = new DbContextOptionsBuilder<PayFlowDbContext>()
            .UseInMemoryDatabase(databaseName: $"RoutingTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PayFlowDbContext(options);
        _healthTracker = new ProviderHealthTracker();
        _routingEngine = new RoutingEngine(_dbContext, _healthTracker);

        // Seed rules
        _dbContext.RoutingRules.AddRange(
            new RoutingRule
            {
                Name = "PromptPay QR Rule",
                Priority = 1,
                PaymentMethod = PaymentMethod.PromptPayQr,
                PrimaryProvider = "Opn",
                FallbackProvider = "GBPrimePay",
                IsEnabled = true
            },
            new RoutingRule
            {
                Name = "High Value Card Rule",
                Priority = 2,
                PaymentMethod = PaymentMethod.CreditCard,
                MinAmount = 10000,
                PrimaryProvider = "GBPrimePay",
                FallbackProvider = "Opn",
                IsEnabled = true
            }
        );
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task ResolveRouteAsync_WithPromptPay_RoutesToOpnWithGBFallback()
    {
        // Arrange
        var request = new PaymentRequestDto
        {
            Amount = 1500,
            Currency = "THB",
            PaymentMethod = PaymentMethod.PromptPayQr
        };

        // Act
        var decision = await _routingEngine.ResolveRouteAsync(_merchantId, request);

        // Assert
        decision.PrimaryProvider.Should().Be("Opn");
        decision.FallbackProvider.Should().Be("GBPrimePay");
        decision.MatchedRuleName.Should().Be("PromptPay QR Rule");
    }

    [Fact]
    public async Task ResolveRouteAsync_WhenPrimaryProviderDegraded_PromotesFallbackToPrimary()
    {
        // Arrange: Simulate 3 consecutive failures for "Opn" within current minute
        await _routingEngine.RecordMetricAsync("Opn", success: false, latencyMs: 3000);
        await _routingEngine.RecordMetricAsync("Opn", success: false, latencyMs: 3000);
        await _routingEngine.RecordMetricAsync("Opn", success: false, latencyMs: 3000);

        var request = new PaymentRequestDto
        {
            Amount = 500,
            Currency = "THB",
            PaymentMethod = PaymentMethod.PromptPayQr
        };

        // Act
        var decision = await _routingEngine.ResolveRouteAsync(_merchantId, request);

        // Assert: GBPrimePay should be promoted to primary because Opn circuit is tripped
        decision.PrimaryProvider.Should().Be("GBPrimePay");
        decision.FallbackProvider.Should().BeNull();
        decision.MatchedRuleName.Should().Contain("Failover");
    }

    [Fact]
    public async Task ResolveRouteAsync_WhenNoRuleMatches_ReturnsSystemDefault()
    {
        // Arrange: LinePay has no rule configured
        var request = new PaymentRequestDto
        {
            Amount = 200,
            Currency = "THB",
            PaymentMethod = PaymentMethod.LinePay
        };

        // Act
        var decision = await _routingEngine.ResolveRouteAsync(_merchantId, request);

        // Assert
        decision.PrimaryProvider.Should().Be("Opn");
        decision.FallbackProvider.Should().Be("GBPrimePay");
        decision.MatchedRuleName.Should().Be("System Default Fallback");
    }
}
