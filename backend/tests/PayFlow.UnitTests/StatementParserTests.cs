using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.DTOs;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure.Parsers;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Services;
using Xunit;

namespace PayFlow.UnitTests;

public class StatementParserTests
{
    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void OpnCsvStatementParser_WithStandardOmiseFormat_ParsesAllFieldsCorrectly()
    {
        // Arrange
        var csv = """
            id,created_at,amount,currency,fee,fee_vat,net,status,description
            chrg_opn_101,2026-09-11 10:15:30,1500.00,THB,20.00,4.75,1475.25,successful,ORD-101
            chrg_opn_102,2026-09-11 11:20:00,2000.00,THB,30.00,7.12,1962.88,successful,ORD-102
            """;
        using var stream = CreateStream(csv);
        var parser = new OpnCsvStatementParser();

        // Act
        var records = parser.ParseStatement(stream, "opn_settlement_2026-09-11.csv").ToList();

        // Assert
        records.Should().HaveCount(2);

        var r1 = records[0];
        r1.ProviderTransactionId.Should().Be("chrg_opn_101");
        r1.MerchantReference.Should().Be("ORD-101");
        r1.Amount.Should().Be(1500.00m);
        r1.Fee.Should().Be(24.75m); // 20.00 + 4.75 VAT
        r1.NetSettlement.Should().Be(1475.25m);
        r1.Status.Should().Be("SUCCESS");
        r1.SettlementDate.Year.Should().Be(2026);
        r1.SettlementDate.Month.Should().Be(9);
        r1.SettlementDate.Day.Should().Be(11);

        var r2 = records[1];
        r2.ProviderTransactionId.Should().Be("chrg_opn_102");
        r2.MerchantReference.Should().Be("ORD-102");
        r2.Amount.Should().Be(2000.00m);
        r2.Fee.Should().Be(37.12m); // 30.00 + 7.12 VAT
        r2.NetSettlement.Should().Be(1962.88m);
        r2.Status.Should().Be("SUCCESS");
    }

    [Fact]
    public void OpnCsvStatementParser_WithFriendlySettlementReport_ComputesNetWhenMissingAndCleansCurrency()
    {
        // Arrange
        var csv = """
            "Transaction ID","Merchant Reference","Amount","Fee","Settlement Date","Status"
            "chrg_opn_201","ORD-201","฿2,500.00","฿41.25","2026-09-11T12:00:00Z","SUCCESS"
            """;
        using var stream = CreateStream(csv);
        var parser = new OpnCsvStatementParser();

        // Act
        var records = parser.ParseStatement(stream, "opn_report.csv").ToList();

        // Assert
        records.Should().HaveCount(1);
        var record = records[0];
        record.ProviderTransactionId.Should().Be("chrg_opn_201");
        record.MerchantReference.Should().Be("ORD-201");
        record.Amount.Should().Be(2500.00m);
        record.Fee.Should().Be(41.25m);
        record.NetSettlement.Should().Be(2458.75m); // 2500 - 41.25 computed automatically
        record.Status.Should().Be("SUCCESS");
    }

    [Fact]
    public void OpnCsvStatementParser_WithFailedAndRefundedTransactions_SetsCorrectStatus()
    {
        // Arrange
        var csv = """
            id,amount,fee,created_at,status,description
            chrg_opn_301,500.00,0.00,2026-09-11,failed,ORD-301
            chrg_opn_302,750.00,12.00,2026-09-11,refunded,ORD-302
            """;
        using var stream = CreateStream(csv);
        var parser = new OpnCsvStatementParser();

        // Act
        var records = parser.ParseStatement(stream, "opn_exceptions.csv").ToList();

        // Assert
        records.Should().HaveCount(2);
        records[0].Status.Should().Be("FAILED");
        records[1].Status.Should().Be("REFUNDED");
    }

    [Fact]
    public void GbPrimePayCsvStatementParser_WithStandardGbpFormat_ParsesAllFieldsCorrectly()
    {
        // Arrange
        var csv = """
            gbpReferenceNo,detail,amount,fee,vat,netAmount,date,resultCode
            GB_20260910101530_1001,ORD-GB-1001,350.00,5.78,0.40,343.82,2026-09-10 10:15:30,00
            GB_20260910112000_1002,ORD-GB-1002,4800.00,79.20,5.54,4715.26,2026-09-10 11:20:00,00
            """;
        using var stream = CreateStream(csv);
        var parser = new GbPrimePayCsvStatementParser();

        // Act
        var records = parser.ParseStatement(stream, "gb_settlement_2026-09-10.csv").ToList();

        // Assert
        records.Should().HaveCount(2);

        var r1 = records[0];
        r1.ProviderTransactionId.Should().Be("GB_20260910101530_1001");
        r1.MerchantReference.Should().Be("ORD-GB-1001");
        r1.Amount.Should().Be(350.00m);
        r1.Fee.Should().Be(6.18m); // 5.78 + 0.40
        r1.NetSettlement.Should().Be(343.82m);
        r1.Status.Should().Be("SUCCESS");

        var r2 = records[1];
        r2.ProviderTransactionId.Should().Be("GB_20260910112000_1002");
        r2.MerchantReference.Should().Be("ORD-GB-1002");
        r2.Amount.Should().Be(4800.00m);
        r2.Fee.Should().Be(84.74m); // 79.20 + 5.54
        r2.NetSettlement.Should().Be(4715.26m);
        r2.Status.Should().Be("SUCCESS");
    }

    [Fact]
    public void GbPrimePayCsvStatementParser_WithFriendlyHeadersAndResultCode_HandlesStatus()
    {
        // Arrange
        var csv = """
            Reference No,Merchant Reference,Amount,Fee,Net Amount,Payment Date,Result Code
            GB_001,ORD-6001,4800.00,84.74,4715.26,2026-09-10 16:30:00,00
            GB_002,ORD-6002,1200.00,21.18,1178.82,2026-09-10 16:35:00,05
            """;
        using var stream = CreateStream(csv);
        var parser = new GbPrimePayCsvStatementParser();

        // Act
        var records = parser.ParseStatement(stream, "gb_custom.csv").ToList();

        // Assert
        records.Should().HaveCount(2);
        records[0].Status.Should().Be("SUCCESS");
        records[1].Status.Should().Be("FAILED"); // ResultCode "05" is error/declined
    }

    [Fact]
    public void AutoDetectStatementParser_WithOpnHeaders_AutoDetectsAndParses()
    {
        // Arrange
        var csv = """
            id,created_at,amount,currency,fee,fee_vat,net,status,description
            chrg_opn_401,2026-09-11 10:00:00,1000.00,THB,16.50,0.00,983.50,successful,ORD-401
            """;
        using var stream = CreateStream(csv);
        var autoParser = new AutoDetectStatementParser();

        // Act
        var provider = autoParser.DetectProvider(stream, "unknown_settlement.csv");
        var records = autoParser.ParseStatement(stream, "unknown_settlement.csv").ToList();

        // Assert
        provider.Should().Be("Opn");
        records.Should().HaveCount(1);
        records[0].ProviderTransactionId.Should().Be("chrg_opn_401");
        records[0].Amount.Should().Be(1000.00m);
    }

    [Fact]
    public void AutoDetectStatementParser_WithGbPrimePayHeaders_AutoDetectsAndParses()
    {
        // Arrange
        var csv = """
            gbpReferenceNo,detail,amount,fee,vat,netAmount,date,resultCode
            GB_20260912_501,ORD-501,500.00,8.25,0.58,491.17,2026-09-12 12:00:00,00
            """;
        using var stream = CreateStream(csv);
        var autoParser = new AutoDetectStatementParser();

        // Act
        var provider = autoParser.DetectProvider(stream, "daily_payout.csv");
        var records = autoParser.ParseStatement(stream, "daily_payout.csv").ToList();

        // Assert
        provider.Should().Be("GBPrimePay");
        records.Should().HaveCount(1);
        records[0].ProviderTransactionId.Should().Be("GB_20260912_501");
        records[0].Amount.Should().Be(500.00m);
    }

    [Fact]
    public void AutoDetectStatementParser_WithGenericHeadersAndOpnTxnId_RoutesToOpn()
    {
        // Arrange
        var csv = """
            Transaction ID,Merchant Reference,Amount,Fee,Net Settlement,Date,Status
            chrg_opn_888,ORD-888,3200.00,52.80,3147.20,2026-09-11 15:00:00,SUCCESS
            """;
        using var stream = CreateStream(csv);
        var autoParser = new AutoDetectStatementParser();

        // Act
        var provider = autoParser.DetectProvider(stream, "settlement_data.csv");
        var records = autoParser.ParseStatement(stream, "settlement_data.csv").ToList();

        // Assert
        provider.Should().Be("Opn");
        records.Should().HaveCount(1);
        records[0].ProviderTransactionId.Should().Be("chrg_opn_888");
        records[0].MerchantReference.Should().Be("ORD-888");
    }

    [Fact]
    public void AutoDetectStatementParser_WithGenericHeadersAndGbTxnId_RoutesToGbPrimePay()
    {
        // Arrange
        var csv = """
            Transaction ID,Merchant Reference,Amount,Fee,Net Settlement,Date,Status
            GB_20260911_999,ORD-999,6500.00,107.25,6392.75,2026-09-11 15:30:00,SUCCESS
            """;
        using var stream = CreateStream(csv);
        var autoParser = new AutoDetectStatementParser();

        // Act
        var provider = autoParser.DetectProvider(stream, "settlement_data.csv");
        var records = autoParser.ParseStatement(stream, "settlement_data.csv").ToList();

        // Assert
        provider.Should().Be("GBPrimePay");
        records.Should().HaveCount(1);
        records[0].ProviderTransactionId.Should().Be("GB_20260911_999");
        records[0].MerchantReference.Should().Be("ORD-999");
    }

    [Fact]
    public void AutoDetectStatementParser_WithFileNameFallback_RoutesCorrectly()
    {
        // Arrange
        var csv = """
            Txn,Ref,Amount,Fee,Date
            1001,REF-01,100.00,2.00,2026-09-11
            """;
        using var stream = CreateStream(csv);
        var autoParser = new AutoDetectStatementParser();

        // Act
        var providerOpn = autoParser.DetectProvider(stream, "opn_batch_2026.csv");
        var providerGb = autoParser.DetectProvider(stream, "gbprime_batch_2026.csv");

        // Assert
        providerOpn.Should().Be("Opn");
        providerGb.Should().Be("GBPrimePay");
    }

    [Fact]
    public void AutoDetectStatementParser_WithUnrecognizedContent_ThrowsInvalidOperationException()
    {
        // Arrange
        var csv = """
            ColA,ColB,ColC
            1,2,3
            """;
        using var stream = CreateStream(csv);
        var autoParser = new AutoDetectStatementParser();

        // Act & Assert
        var act = () => autoParser.ParseStatement(stream, "unknown.csv").ToList();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unable to automatically detect statement format*");
    }

    [Fact]
    public async Task EndToEnd_ParserWithReconciliationEngine_ReconcilesSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PayFlowDbContext>()
            .UseInMemoryDatabase(databaseName: $"E2ETestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new PayFlowDbContext(options);
        var merchantId = Guid.NewGuid();

        dbContext.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            MerchantReference = "ORD-E2E-1",
            ProviderTransactionId = "chrg_opn_e2e_1",
            Amount = 1500.00m,
            Status = PaymentStatus.Success
        });
        await dbContext.SaveChangesAsync();

        var csv = """
            id,created_at,amount,currency,fee,fee_vat,net,status,description
            chrg_opn_e2e_1,2026-09-11 12:00:00,1500.00,THB,20.00,4.75,1475.25,successful,ORD-E2E-1
            chrg_opn_e2e_2,2026-09-11 12:05:00,2200.00,THB,33.00,0.00,2167.00,successful,ORD-E2E-2
            """;
        using var stream = CreateStream(csv);
        var autoParser = new AutoDetectStatementParser();
        var reconcileEngine = new ReconciliationEngine(dbContext);

        // Act: 1. Auto-detect and parse
        var detectedProvider = autoParser.DetectProvider(stream, "statement_opn.csv");
        var statementRecords = autoParser.ParseStatement(stream, "statement_opn.csv");

        // Act: 2. Process reconciliation batch
        var batch = await reconcileEngine.ProcessStatementBatchAsync(
            merchantId, detectedProvider, "statement_opn.csv", statementRecords);

        // Assert
        detectedProvider.Should().Be("Opn");
        batch.TotalRecords.Should().Be(2);
        batch.MatchedCount.Should().Be(1);
        batch.UnmatchedCount.Should().Be(1);
        batch.DiscrepancyCount.Should().Be(1);

        var matchedItem = batch.Items.First(i => i.ProviderTransactionId == "chrg_opn_e2e_1");
        matchedItem.Status.Should().Be(ReconcileStatus.Matched);
        matchedItem.NetSettlement.Should().Be(1475.25m);

        var unmatchedItem = batch.Items.First(i => i.ProviderTransactionId == "chrg_opn_e2e_2");
        unmatchedItem.Status.Should().Be(ReconcileStatus.Unmatched);
    }
}
