using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.DTOs;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Services;
using Xunit;

namespace PayFlow.UnitTests;

public class ReconciliationEngineTests
{
    private readonly PayFlowDbContext _dbContext;
    private readonly ReconciliationEngine _reconciliationEngine;
    private readonly Guid _merchantId = Guid.NewGuid();

    public ReconciliationEngineTests()
    {
        var options = new DbContextOptionsBuilder<PayFlowDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReconcileTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PayFlowDbContext(options);
        _reconciliationEngine = new ReconciliationEngine(_dbContext);

        // Seed sample transactions in internal ledger
        _dbContext.PaymentTransactions.AddRange(
            new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                MerchantId = _merchantId,
                MerchantReference = "ORD-101",
                ProviderTransactionId = "chrg_opn_101",
                Amount = 1500.00m,
                Status = PaymentStatus.Success
            },
            new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                MerchantId = _merchantId,
                MerchantReference = "ORD-102",
                ProviderTransactionId = "chrg_opn_102",
                Amount = 2000.00m,
                Status = PaymentStatus.Success
            }
        );
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task ProcessStatementBatchAsync_WithExactMatchingRecords_MarksAsMatched()
    {
        // Arrange
        var records = new List<StatementRecordDto>
        {
            new()
            {
                ProviderTransactionId = "chrg_opn_101",
                MerchantReference = "ORD-101",
                Amount = 1500.00m,
                Fee = 24.75m,
                NetSettlement = 1475.25m,
                SettlementDate = DateTimeOffset.UtcNow
            }
        };

        // Act
        var batch = await _reconciliationEngine.ProcessStatementBatchAsync(
            _merchantId, "Opn", "settlement_20260911.csv", records);

        // Assert
        batch.TotalRecords.Should().Be(1);
        batch.MatchedCount.Should().Be(1);
        batch.UnmatchedCount.Should().Be(0);
        batch.DiscrepancyCount.Should().Be(0);
        batch.Items[0].Status.Should().Be(ReconcileStatus.Matched);
    }

    [Fact]
    public async Task ProcessStatementBatchAsync_WithUnmatchedAndAmountMismatchRecords_CorrectlyClassifies()
    {
        // Arrange
        var records = new List<StatementRecordDto>
        {
            // 1. Amount mismatch for ORD-102 (Statement says 1800, internal says 2000)
            new()
            {
                ProviderTransactionId = "chrg_opn_102",
                MerchantReference = "ORD-102",
                Amount = 1800.00m,
                Fee = 29.70m,
                NetSettlement = 1770.30m,
                SettlementDate = DateTimeOffset.UtcNow
            },
            // 2. Unknown transaction from provider (ghost transaction)
            new()
            {
                ProviderTransactionId = "chrg_opn_999",
                MerchantReference = "ORD-UNKNOWN",
                Amount = 500.00m,
                Fee = 8.25m,
                NetSettlement = 491.75m,
                SettlementDate = DateTimeOffset.UtcNow
            }
        };

        // Act
        var batch = await _reconciliationEngine.ProcessStatementBatchAsync(
            _merchantId, "Opn", "settlement_anomalies.csv", records);

        // Assert
        batch.TotalRecords.Should().Be(2);
        batch.MatchedCount.Should().Be(0);
        batch.UnmatchedCount.Should().Be(1);
        batch.DiscrepancyCount.Should().Be(2);

        var amountMismatchItem = batch.Items.First(i => i.ProviderTransactionId == "chrg_opn_102");
        amountMismatchItem.Status.Should().Be(ReconcileStatus.AmountMismatch);
        amountMismatchItem.DiscrepancyNote.Should().Contain("Amount mismatch");

        var unmatchedItem = batch.Items.First(i => i.ProviderTransactionId == "chrg_opn_999");
        unmatchedItem.Status.Should().Be(ReconcileStatus.Unmatched);
        unmatchedItem.DiscrepancyNote.Should().Contain("not found");
    }
}
