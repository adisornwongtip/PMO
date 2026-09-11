using PayFlow.Application.DTOs;
using PayFlow.Application.Models;
using PayFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace PayFlow.Application.Interfaces;

public interface IPaymentProviderAdapter
{
    string ProviderCode { get; }
    Task<ProviderPaymentResult> ChargeAsync(PaymentRequestDto request, CancellationToken ct = default);
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}

public interface IRoutingEngine
{
    Task<RoutingDecision> ResolveRouteAsync(Guid merchantId, PaymentRequestDto request, CancellationToken ct = default);
    Task RecordMetricAsync(string providerCode, bool success, long latencyMs);
}

public interface IReconciliationService
{
    Task<ReconciliationBatch> ProcessStatementBatchAsync(
        Guid merchantId,
        string providerCode,
        string fileName,
        IEnumerable<StatementRecordDto> records,
        CancellationToken ct = default);
}

public interface IPaymentOrchestrationService
{
    Task<PaymentResponseDto> ProcessPaymentAsync(
        Guid merchantId,
        string idempotencyKey,
        PaymentRequestDto request,
        CancellationToken ct = default);
}

public interface IPayFlowDbContext
{
    DbSet<Merchant> Merchants { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<RoutingAttemptLog> RoutingAttemptLogs { get; }
    DbSet<RoutingRule> RoutingRules { get; }
    DbSet<ReconciliationBatch> ReconciliationBatches { get; }
    DbSet<ReconciliationItem> ReconciliationItems { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
