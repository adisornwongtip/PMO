using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Models;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Webhooks;
using Xunit;

namespace PayFlow.UnitTests;

public class WebhookProcessorTests
{
    private readonly PayFlowDbContext _dbContext;
    private readonly OpnWebhookProcessor _opnProcessor;
    private readonly GbPrimePayWebhookProcessor _gbProcessor;
    private readonly Guid _merchantId = Guid.NewGuid();
    private const string WebhookSecret = "whsec_test_secret_123456789";

    public WebhookProcessorTests()
    {
        var options = new DbContextOptionsBuilder<PayFlowDbContext>()
            .UseInMemoryDatabase(databaseName: $"WebhookTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PayFlowDbContext(options);

        // Seed Merchant
        _dbContext.Merchants.Add(new Merchant
        {
            Id = _merchantId,
            Name = "Test Merchant",
            ApiKey = "pk_test_123",
            ApiSecret = "sk_test_456",
            WebhookSecret = WebhookSecret,
            IsActive = true
        });
        _dbContext.SaveChanges();

        _opnProcessor = new OpnWebhookProcessor(_dbContext);
        _gbProcessor = new GbPrimePayWebhookProcessor(_dbContext);
    }

    // ---------------------------------------------------------
    // OPN WEBHOOK PROCESSOR TESTS
    // ---------------------------------------------------------

    [Fact]
    public void OpnWebhookProcessor_ValidateSignature_WithValidHmac_ReturnsTrue()
    {
        // Arrange
        var body = "{\"object\":\"event\",\"key\":\"charge.complete\"}";
        var signature = ComputeHmacSha256Hex(body, WebhookSecret);

        var payload = new WebhookPayload
        {
            RawBody = body,
            Signature = signature
        };

        // Act
        var isValid = _opnProcessor.ValidateSignature(payload, WebhookSecret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void OpnWebhookProcessor_ValidateSignature_WithHeaderAndPrefix_ReturnsTrue()
    {
        // Arrange
        var body = "{\"object\":\"event\",\"key\":\"charge.complete\"}";
        var signature = "sha256=" + ComputeHmacSha256Hex(body, WebhookSecret);

        var payload = new WebhookPayload
        {
            RawBody = body,
            Headers = new Dictionary<string, string> { { "X-Opn-Signature", signature } }
        };

        // Act
        var isValid = _opnProcessor.ValidateSignature(payload, WebhookSecret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void OpnWebhookProcessor_ValidateSignature_WithTamperedBody_ReturnsFalse()
    {
        // Arrange
        var body = "{\"object\":\"event\",\"key\":\"charge.complete\"}";
        var signature = ComputeHmacSha256Hex(body, WebhookSecret);

        var tamperedPayload = new WebhookPayload
        {
            RawBody = "{\"object\":\"event\",\"key\":\"charge.complete\",\"tampered\":true}",
            Signature = signature
        };

        // Act
        var isValid = _opnProcessor.ValidateSignature(tamperedPayload, WebhookSecret);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task OpnWebhookProcessor_ProcessWebhookAsync_ChargeComplete_UpdatesTransactionToSuccessAndSetsCompletedAt()
    {
        // Arrange
        var chargeId = "chrg_opn_test_abc123";
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            MerchantId = _merchantId,
            MerchantReference = "ORD-OPN-100",
            Amount = 1500,
            Currency = "THB",
            PaymentMethod = PaymentMethod.PromptPayQr,
            SelectedProvider = "Opn",
            ProviderTransactionId = chargeId,
            Status = PaymentStatus.Processing
        };
        _dbContext.PaymentTransactions.Add(txn);
        await _dbContext.SaveChangesAsync();

        var body = $$"""
        {
            "object": "event",
            "key": "charge.complete",
            "data": {
                "object": "charge",
                "id": "{{chargeId}}",
                "status": "successful",
                "paid": true,
                "amount": 150000,
                "currency": "thb"
            }
        }
        """;

        var signature = ComputeHmacSha256Hex(body, WebhookSecret);
        var payload = new WebhookPayload
        {
            RawBody = body,
            Signature = signature
        };

        // Act
        var result = await _opnProcessor.ProcessWebhookAsync(payload, WebhookSecret);

        // Assert
        result.Success.Should().BeTrue();
        result.NewStatus.Should().Be(PaymentStatus.Success);
        result.TransactionId.Should().Be(txn.Id);

        var updatedTxn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == txn.Id);
        updatedTxn.Should().NotBeNull();
        updatedTxn!.Status.Should().Be(PaymentStatus.Success);
        updatedTxn.CompletedAt.Should().NotBeNull();
        updatedTxn.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OpnWebhookProcessor_ProcessWebhookAsync_InvalidSignature_ReturnsFailureWithoutUpdating()
    {
        // Arrange
        var chargeId = "chrg_opn_test_sig_fail";
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            MerchantId = _merchantId,
            MerchantReference = "ORD-OPN-FAIL",
            Amount = 500,
            SelectedProvider = "Opn",
            ProviderTransactionId = chargeId,
            Status = PaymentStatus.Processing
        };
        _dbContext.PaymentTransactions.Add(txn);
        await _dbContext.SaveChangesAsync();

        var body = $"{{\"object\":\"event\",\"key\":\"charge.complete\",\"data\":{{\"id\":\"{chargeId}\",\"status\":\"successful\"}}}}";
        var payload = new WebhookPayload
        {
            RawBody = body,
            Signature = "invalid_bogus_signature_hex"
        };

        // Act
        var result = await _opnProcessor.ProcessWebhookAsync(payload, WebhookSecret);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid");

        var unaffectedTxn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == txn.Id);
        unaffectedTxn!.Status.Should().Be(PaymentStatus.Processing);
        unaffectedTxn.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task OpnWebhookProcessor_ProcessWebhookAsync_WhenTransactionNotFound_ReturnsFailedResult()
    {
        // Arrange
        var body = """
        {
            "object": "event",
            "key": "charge.complete",
            "data": {
                "id": "chrg_non_existent_999",
                "status": "successful",
                "paid": true
            }
        }
        """;
        var signature = ComputeHmacSha256Hex(body, WebhookSecret);
        var payload = new WebhookPayload { RawBody = body, Signature = signature };

        // Act
        var result = await _opnProcessor.ProcessWebhookAsync(payload, WebhookSecret);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task OpnWebhookProcessor_ProcessWebhookAsync_IdempotentOnDuplicateCall_ReturnsSuccess()
    {
        // Arrange
        var chargeId = "chrg_opn_idem_test";
        var completedTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            MerchantId = _merchantId,
            MerchantReference = "ORD-OPN-IDEM",
            Amount = 2000,
            SelectedProvider = "Opn",
            ProviderTransactionId = chargeId,
            Status = PaymentStatus.Success,
            CompletedAt = completedTime
        };
        _dbContext.PaymentTransactions.Add(txn);
        await _dbContext.SaveChangesAsync();

        var body = $$"""
        {
            "object": "event",
            "key": "charge.complete",
            "data": { "id": "{{chargeId}}", "status": "successful", "paid": true }
        }
        """;
        var signature = ComputeHmacSha256Hex(body, WebhookSecret);
        var payload = new WebhookPayload { RawBody = body, Signature = signature };

        // Act
        var result = await _opnProcessor.ProcessWebhookAsync(payload, WebhookSecret);

        // Assert
        result.Success.Should().BeTrue();
        result.NewStatus.Should().Be(PaymentStatus.Success);

        var verifyTxn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == txn.Id);
        verifyTxn!.CompletedAt.Should().Be(completedTime); // CompletedAt unchanged
    }

    // ---------------------------------------------------------
    // GB PRIMEPAY WEBHOOK PROCESSOR TESTS
    // ---------------------------------------------------------

    [Fact]
    public void GbPrimePayWebhookProcessor_ValidateSignature_WithValidHmac_ReturnsTrue()
    {
        // Arrange
        var body = "{\"referenceNo\":\"ORD-GB-01\",\"gbpReferenceNo\":\"GB_123\",\"resultCode\":\"00\"}";
        var signature = ComputeHmacSha256Hex(body, WebhookSecret);

        var payload = new WebhookPayload
        {
            RawBody = body,
            Headers = new Dictionary<string, string> { { "X-GB-Signature", signature } }
        };

        // Act
        var isValid = _gbProcessor.ValidateSignature(payload, WebhookSecret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void GbPrimePayWebhookProcessor_ValidateSignature_WithInvalidChecksum_ReturnsFalse()
    {
        // Arrange
        var body = "{\"referenceNo\":\"ORD-GB-02\",\"resultCode\":\"00\"}";
        var payload = new WebhookPayload
        {
            RawBody = body,
            Signature = "bad_checksum_hash"
        };

        // Act
        var isValid = _gbProcessor.ValidateSignature(payload, WebhookSecret);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task GbPrimePayWebhookProcessor_ProcessWebhookAsync_ResultCode00_UpdatesTransactionToSuccessAndSetsCompletedAt()
    {
        // Arrange
        var gbpRef = "GB_20260912120000_5678";
        var merchantRef = "ORD-GB-SUCCESS-01";
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            MerchantId = _merchantId,
            MerchantReference = merchantRef,
            Amount = 3500,
            Currency = "THB",
            PaymentMethod = PaymentMethod.CreditCard,
            SelectedProvider = "GBPrimePay",
            ProviderTransactionId = gbpRef,
            Status = PaymentStatus.Processing
        };
        _dbContext.PaymentTransactions.Add(txn);
        await _dbContext.SaveChangesAsync();

        var body = $$"""
        {
            "referenceNo": "{{merchantRef}}",
            "gbpReferenceNo": "{{gbpRef}}",
            "amount": 3500.00,
            "resultCode": "00",
            "resultMessage": "Success",
            "status": "S"
        }
        """;

        var signature = ComputeHmacSha256Hex(body, WebhookSecret);
        var payload = new WebhookPayload
        {
            RawBody = body,
            Headers = new Dictionary<string, string> { { "X-GB-Signature", signature } }
        };

        // Act
        var result = await _gbProcessor.ProcessWebhookAsync(payload, WebhookSecret);

        // Assert
        result.Success.Should().BeTrue();
        result.NewStatus.Should().Be(PaymentStatus.Success);
        result.TransactionId.Should().Be(txn.Id);

        var updatedTxn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == txn.Id);
        updatedTxn.Should().NotBeNull();
        updatedTxn!.Status.Should().Be(PaymentStatus.Success);
        updatedTxn.CompletedAt.Should().NotBeNull();
        updatedTxn.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GbPrimePayWebhookProcessor_ProcessWebhookAsync_ResultCodeFailed_UpdatesTransactionToFailed()
    {
        // Arrange
        var gbpRef = "GB_20260912120000_FAIL";
        var merchantRef = "ORD-GB-FAIL-01";
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            MerchantId = _merchantId,
            MerchantReference = merchantRef,
            Amount = 1200,
            Currency = "THB",
            SelectedProvider = "GBPrimePay",
            ProviderTransactionId = gbpRef,
            Status = PaymentStatus.Processing
        };
        _dbContext.PaymentTransactions.Add(txn);
        await _dbContext.SaveChangesAsync();

        var body = $$"""
        {
            "referenceNo": "{{merchantRef}}",
            "gbpReferenceNo": "{{gbpRef}}",
            "amount": 1200.00,
            "resultCode": "01",
            "resultMessage": "Insufficient credit limit",
            "status": "F"
        }
        """;

        var signature = ComputeHmacSha256Hex(body, WebhookSecret);
        var payload = new WebhookPayload
        {
            RawBody = body,
            Signature = signature
        };

        // Act
        var result = await _gbProcessor.ProcessWebhookAsync(payload, WebhookSecret);

        // Assert
        result.Success.Should().BeTrue(); // Processing completed
        result.NewStatus.Should().Be(PaymentStatus.Failed);

        var updatedTxn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == txn.Id);
        updatedTxn!.Status.Should().Be(PaymentStatus.Failed);
        updatedTxn.FailureReason.Should().Contain("Insufficient credit limit");
        updatedTxn.CompletedAt.Should().NotBeNull();
    }

    // ---------------------------------------------------------
    // HELPER
    // ---------------------------------------------------------

    private static string ComputeHmacSha256Hex(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
