using Microsoft.EntityFrameworkCore;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Infrastructure.Services;

public class ReconciliationEngine : IReconciliationService
{
    private readonly IPayFlowDbContext _dbContext;

    public ReconciliationEngine(IPayFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReconciliationBatch> ProcessStatementBatchAsync(
        Guid merchantId,
        string providerCode,
        string fileName,
        IEnumerable<StatementRecordDto> records,
        CancellationToken ct = default)
    {
        var recordList = records.ToList();
        var batch = new ReconciliationBatch
        {
            MerchantId = merchantId,
            ProviderCode = providerCode,
            FileName = fileName,
            TotalRecords = recordList.Count,
            UploadedAt = DateTimeOffset.UtcNow
        };

        // Preload recent transactions for matching
        var transactions = await _dbContext.PaymentTransactions
            .Where(t => t.MerchantId == merchantId)
            .ToListAsync(ct);

        int matchedCount = 0;
        int unmatchedCount = 0;
        int discrepancyCount = 0;
        decimal totalSettled = 0;

        foreach (var record in recordList)
        {
            totalSettled += record.NetSettlement;

            // Match by ProviderTransactionId or MerchantReference
            var internalTxn = transactions.FirstOrDefault(t =>
                (!string.IsNullOrEmpty(t.ProviderTransactionId) && string.Equals(t.ProviderTransactionId, record.ProviderTransactionId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(t.MerchantReference) && string.Equals(t.MerchantReference, record.MerchantReference, StringComparison.OrdinalIgnoreCase))
            );

            var item = new ReconciliationItem
            {
                BatchId = batch.Id,
                ProviderTransactionId = record.ProviderTransactionId,
                MerchantReference = record.MerchantReference,
                StatementAmount = record.Amount,
                Fee = record.Fee,
                NetSettlement = record.NetSettlement
            };

            if (internalTxn == null)
            {
                item.Status = ReconcileStatus.Unmatched;
                item.DiscrepancyNote = "Transaction not found in PayFlow internal ledger.";
                unmatchedCount++;
                discrepancyCount++;
            }
            else
            {
                item.InternalTransactionId = internalTxn.Id;
                item.InternalAmount = internalTxn.Amount;

                if (Math.Abs(internalTxn.Amount - record.Amount) > 0.001m)
                {
                    item.Status = ReconcileStatus.AmountMismatch;
                    item.DiscrepancyNote = $"Amount mismatch: Internal ฿{internalTxn.Amount:F2} vs Statement ฿{record.Amount:F2}";
                    discrepancyCount++;
                }
                else if (internalTxn.Status != PaymentStatus.Success)
                {
                    item.Status = ReconcileStatus.FeeDiscrepancy;
                    item.DiscrepancyNote = $"Status variance: Internal is '{internalTxn.Status}', but settled by provider.";
                    discrepancyCount++;
                }
                else
                {
                    item.Status = ReconcileStatus.Matched;
                    matchedCount++;
                }
            }

            batch.Items.Add(item);
        }

        batch.MatchedCount = matchedCount;
        batch.UnmatchedCount = unmatchedCount;
        batch.DiscrepancyCount = discrepancyCount;
        batch.TotalSettledAmount = totalSettled;

        _dbContext.ReconciliationBatches.Add(batch);
        await _dbContext.SaveChangesAsync(ct);

        return batch;
    }
}
