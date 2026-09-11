using Microsoft.EntityFrameworkCore;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Infrastructure.Services;

public class PaymentOrchestrationService : IPaymentOrchestrationService
{
    private readonly IPayFlowDbContext _dbContext;
    private readonly IRoutingEngine _routingEngine;
    private readonly IEnumerable<IPaymentProviderAdapter> _adapters;

    public PaymentOrchestrationService(
        IPayFlowDbContext dbContext,
        IRoutingEngine routingEngine,
        IEnumerable<IPaymentProviderAdapter> adapters)
    {
        _dbContext = dbContext;
        _routingEngine = routingEngine;
        _adapters = adapters;
    }

    public async Task<PaymentResponseDto> ProcessPaymentAsync(
        Guid merchantId,
        string idempotencyKey,
        PaymentRequestDto request,
        CancellationToken ct = default)
    {
        // 1. Idempotency check
        var existingTxn = await _dbContext.PaymentTransactions
            .Include(t => t.RoutingLogs)
            .FirstOrDefaultAsync(t => t.MerchantId == merchantId && t.IdempotencyKey == idempotencyKey, ct);

        if (existingTxn != null)
        {
            return MapToResponse(existingTxn);
        }

        // 2. Resolve Smart Route
        var route = await _routingEngine.ResolveRouteAsync(merchantId, request, ct);

        var txn = new PaymentTransaction
        {
            MerchantId = merchantId,
            MerchantReference = request.MerchantReference,
            IdempotencyKey = idempotencyKey,
            Amount = request.Amount,
            Currency = request.Currency,
            PaymentMethod = request.PaymentMethod,
            Status = PaymentStatus.Processing,
            SelectedProvider = route.PrimaryProvider,
            AttemptsCount = 1
        };

        // 3. Attempt Primary Provider
        var primaryAdapter = GetAdapter(route.PrimaryProvider);
        var primaryResult = await primaryAdapter.ChargeAsync(request, ct);

        await _routingEngine.RecordMetricAsync(primaryAdapter.ProviderCode, primaryResult.Success, primaryResult.LatencyMs);

        txn.RoutingLogs.Add(new RoutingAttemptLog
        {
            PaymentTransactionId = txn.Id,
            ProviderCode = primaryAdapter.ProviderCode,
            Success = primaryResult.Success,
            StatusCode = primaryResult.RawStatusCode,
            ErrorMessage = primaryResult.ErrorMessage,
            LatencyMs = primaryResult.LatencyMs
        });

        // 4. Handle Success
        if (primaryResult.Success)
        {
            txn.Status = PaymentStatus.Success;
            txn.ProviderTransactionId = primaryResult.ProviderTransactionId;
            txn.CompletedAt = DateTimeOffset.UtcNow;

            _dbContext.PaymentTransactions.Add(txn);
            await _dbContext.SaveChangesAsync(ct);

            var resp = MapToResponse(txn);
            resp.QrCodeData = primaryResult.QrCodeData;
            resp.RedirectUrl = primaryResult.RedirectUrl;
            return resp;
        }

        // 5. Automatic Failover to Fallback Provider if Primary failed with retryable error
        if (primaryResult.IsRetryable && !string.IsNullOrEmpty(route.FallbackProvider))
        {
            var fallbackAdapter = GetAdapter(route.FallbackProvider);
            txn.AttemptsCount++;
            txn.SelectedProvider = fallbackAdapter.ProviderCode;

            var fallbackResult = await fallbackAdapter.ChargeAsync(request, ct);
            await _routingEngine.RecordMetricAsync(fallbackAdapter.ProviderCode, fallbackResult.Success, fallbackResult.LatencyMs);

            txn.RoutingLogs.Add(new RoutingAttemptLog
            {
                PaymentTransactionId = txn.Id,
                ProviderCode = fallbackAdapter.ProviderCode,
                Success = fallbackResult.Success,
                StatusCode = fallbackResult.RawStatusCode,
                ErrorMessage = fallbackResult.ErrorMessage,
                LatencyMs = fallbackResult.LatencyMs
            });

            if (fallbackResult.Success)
            {
                txn.Status = PaymentStatus.Success;
                txn.ProviderTransactionId = fallbackResult.ProviderTransactionId;
                txn.CompletedAt = DateTimeOffset.UtcNow;

                _dbContext.PaymentTransactions.Add(txn);
                await _dbContext.SaveChangesAsync(ct);

                var resp = MapToResponse(txn);
                resp.QrCodeData = fallbackResult.QrCodeData;
                resp.RedirectUrl = fallbackResult.RedirectUrl;
                return resp;
            }
        }

        // 6. Non-retryable failure or all providers failed
        txn.Status = PaymentStatus.Failed;
        txn.FailureReason = primaryResult.ErrorMessage ?? "Payment processing failed";
        txn.CompletedAt = DateTimeOffset.UtcNow;

        _dbContext.PaymentTransactions.Add(txn);
        await _dbContext.SaveChangesAsync(ct);

        return MapToResponse(txn);
    }

    private IPaymentProviderAdapter GetAdapter(string providerCode)
    {
        var adapter = _adapters.FirstOrDefault(a => string.Equals(a.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase));
        if (adapter == null)
        {
            throw new InvalidOperationException($"Provider adapter for '{providerCode}' not registered.");
        }
        return adapter;
    }

    private static PaymentResponseDto MapToResponse(PaymentTransaction txn)
    {
        return new PaymentResponseDto
        {
            TransactionId = txn.Id,
            MerchantReference = txn.MerchantReference,
            Status = txn.Status,
            Amount = txn.Amount,
            Currency = txn.Currency,
            ProviderCode = txn.SelectedProvider,
            ProviderTransactionId = txn.ProviderTransactionId,
            AttemptCount = txn.AttemptsCount,
            FailureReason = txn.FailureReason,
            CreatedAt = txn.CreatedAt,
            CompletedAt = txn.CompletedAt
        };
    }
}
