using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Application.Models;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure.Adapters;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Services;
using Xunit;

namespace PayFlow.UnitTests;

public class PaymentOrchestrationServiceTests
{
    private readonly PayFlowDbContext _dbContext;
    private readonly Mock<IRoutingEngine> _mockRouting;
    private readonly MockSandboxAdapter _mockSandbox;
    private readonly OpnAdapter _mockOpn;
    private readonly GbPrimePayAdapter _mockGb;
    private readonly PaymentOrchestrationService _orchestrationService;
    private readonly Guid _merchantId = Guid.NewGuid();

    public PaymentOrchestrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<PayFlowDbContext>()
            .UseInMemoryDatabase(databaseName: $"OrchestrationTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PayFlowDbContext(options);
        _mockRouting = new Mock<IRoutingEngine>();
        _mockSandbox = new MockSandboxAdapter();
        _mockOpn = new OpnAdapter();
        _mockGb = new GbPrimePayAdapter();

        var adapters = new IPaymentProviderAdapter[] { _mockSandbox, _mockOpn, _mockGb };
        _orchestrationService = new PaymentOrchestrationService(_dbContext, _mockRouting.Object, adapters);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenPrimarySucceeds_ReturnsSuccessResponse()
    {
        // Arrange
        _mockRouting.Setup(r => r.ResolveRouteAsync(It.IsAny<Guid>(), It.IsAny<PaymentRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingDecision { PrimaryProvider = "Opn", FallbackProvider = "GBPrimePay" });

        var request = new PaymentRequestDto
        {
            MerchantReference = "ORD-001",
            Amount = 1000,
            Currency = "THB",
            PaymentMethod = PaymentMethod.PromptPayQr
        };

        // Act
        var response = await _orchestrationService.ProcessPaymentAsync(_merchantId, "idemp-001", request);

        // Assert
        response.Status.Should().Be(PaymentStatus.Success);
        response.ProviderCode.Should().Be("Opn");
        response.ProviderTransactionId.Should().StartWith("chrg_opn_");
        response.AttemptCount.Should().Be(1);

        var saved = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == response.TransactionId);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(PaymentStatus.Success);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenPrimaryTimesOut_AutomaticallyFailsOverToFallbackProvider()
    {
        // Arrange: Opn has downtime (HTTP 503 / Timeout)
        _mockOpn.SimulateDowntime = true;

        _mockRouting.Setup(r => r.ResolveRouteAsync(It.IsAny<Guid>(), It.IsAny<PaymentRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingDecision { PrimaryProvider = "Opn", FallbackProvider = "GBPrimePay" });

        var request = new PaymentRequestDto
        {
            MerchantReference = "ORD-002",
            Amount = 2500,
            Currency = "THB",
            PaymentMethod = PaymentMethod.PromptPayQr
        };

        // Act
        var response = await _orchestrationService.ProcessPaymentAsync(_merchantId, "idemp-002", request);

        // Assert: Seamless failover to GBPrimePay!
        response.Status.Should().Be(PaymentStatus.Success);
        response.ProviderCode.Should().Be("GBPrimePay");
        response.ProviderTransactionId.Should().StartWith("GB_");
        response.AttemptCount.Should().Be(2);

        var saved = await _dbContext.PaymentTransactions.Include(t => t.RoutingLogs)
            .FirstOrDefaultAsync(t => t.Id == response.TransactionId);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(PaymentStatus.Success);
        saved.RoutingLogs.Should().HaveCount(2);
        saved.RoutingLogs[0].Success.Should().BeFalse(); // Opn failed
        saved.RoutingLogs[1].Success.Should().BeTrue();  // GB succeeded
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenCardDeclined_DoesNotFailoverToSecondary()
    {
        // Arrange: Primary is MockSandbox with client rejection (Card Declined / Insufficient funds)
        _mockSandbox.SimulateDeclined = true;

        _mockRouting.Setup(r => r.ResolveRouteAsync(It.IsAny<Guid>(), It.IsAny<PaymentRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingDecision { PrimaryProvider = "MockSandbox", FallbackProvider = "Opn" });

        var request = new PaymentRequestDto
        {
            MerchantReference = "ORD-003",
            Amount = 500,
            Currency = "THB",
            PaymentMethod = PaymentMethod.CreditCard
        };

        // Act
        var response = await _orchestrationService.ProcessPaymentAsync(_merchantId, "idemp-003", request);

        // Assert: Rejection is terminal, no failover
        response.Status.Should().Be(PaymentStatus.Failed);
        response.FailureReason.Should().Contain("Card declined");
        response.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WithSameIdempotencyKey_ReturnsCachedTransactionWithoutRecharging()
    {
        // Arrange
        _mockRouting.Setup(r => r.ResolveRouteAsync(It.IsAny<Guid>(), It.IsAny<PaymentRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingDecision { PrimaryProvider = "Opn" });

        var request = new PaymentRequestDto
        {
            MerchantReference = "ORD-DUPLICATE",
            Amount = 300,
            Currency = "THB",
            PaymentMethod = PaymentMethod.PromptPayQr
        };

        // First call
        var resp1 = await _orchestrationService.ProcessPaymentAsync(_merchantId, "idemp-same-key", request);

        // Second call with exact same idempotency key
        var resp2 = await _orchestrationService.ProcessPaymentAsync(_merchantId, "idemp-same-key", request);

        // Assert: Same transaction returned, no duplicate charge
        resp2.TransactionId.Should().Be(resp1.TransactionId);
        resp2.ProviderTransactionId.Should().Be(resp1.ProviderTransactionId);

        var count = await _dbContext.PaymentTransactions.CountAsync(t => t.MerchantId == _merchantId);
        count.Should().Be(1);
    }
}
