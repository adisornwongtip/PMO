using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Application.Models;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Services;

public interface IProviderHealthTracker
{
    bool IsHealthy(string providerCode);
    void RecordMetric(string providerCode, bool success, long latencyMs);
    void Reset();
}

public class ProviderHealthTracker : IProviderHealthTracker
{
    private readonly ConcurrentDictionary<string, ProviderHealthState> _metrics = new();

    public bool IsHealthy(string providerCode)
    {
        if (_metrics.TryGetValue(providerCode, out var state))
        {
            lock (state)
            {
                if (state.ConsecutiveFailures >= 3 && DateTimeOffset.UtcNow - state.LastFailureTime < TimeSpan.FromMinutes(1))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void RecordMetric(string providerCode, bool success, long latencyMs)
    {
        var state = _metrics.GetOrAdd(providerCode, _ => new ProviderHealthState());
        lock (state)
        {
            state.TotalRequests++;
            if (!success)
            {
                state.ConsecutiveFailures++;
                state.LastFailureTime = DateTimeOffset.UtcNow;
            }
            else
            {
                state.ConsecutiveFailures = 0;
            }
            state.TotalLatencyMs += latencyMs;
        }
    }

    public void Reset()
    {
        _metrics.Clear();
    }

    private class ProviderHealthState
    {
        public int TotalRequests { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? LastFailureTime { get; set; }
        public long TotalLatencyMs { get; set; }
    }
}

public class RoutingEngine : IRoutingEngine
{
    private readonly IPayFlowDbContext _dbContext;
    private readonly IProviderHealthTracker _healthTracker;

    public RoutingEngine(IPayFlowDbContext dbContext, IProviderHealthTracker healthTracker)
    {
        _dbContext = dbContext;
        _healthTracker = healthTracker;
    }

    public async Task<RoutingDecision> ResolveRouteAsync(Guid merchantId, PaymentRequestDto request, CancellationToken ct = default)
    {
        var rules = await _dbContext.RoutingRules
            .Where(r => r.IsEnabled && (r.MerchantId == merchantId || r.MerchantId == null))
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        foreach (var rule in rules)
        {
            if (rule.PaymentMethod.HasValue && rule.PaymentMethod.Value != request.PaymentMethod)
                continue;

            if (rule.MinAmount.HasValue && request.Amount < rule.MinAmount.Value)
                continue;

            if (rule.MaxAmount.HasValue && request.Amount > rule.MaxAmount.Value)
                continue;

            if (!string.IsNullOrEmpty(rule.Currency) && !string.Equals(rule.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
                continue;

            var isPrimaryHealthy = _healthTracker.IsHealthy(rule.PrimaryProvider);
            if (isPrimaryHealthy)
            {
                return new RoutingDecision
                {
                    PrimaryProvider = rule.PrimaryProvider,
                    FallbackProvider = rule.FallbackProvider,
                    MatchedRuleName = rule.Name
                };
            }

            if (!string.IsNullOrEmpty(rule.FallbackProvider))
            {
                return new RoutingDecision
                {
                    PrimaryProvider = rule.FallbackProvider,
                    FallbackProvider = null,
                    MatchedRuleName = $"{rule.Name} (Failover: {rule.PrimaryProvider} degraded)"
                };
            }
        }

        return new RoutingDecision
        {
            PrimaryProvider = "Opn",
            FallbackProvider = "GBPrimePay",
            MatchedRuleName = "System Default Fallback"
        };
    }

    public Task RecordMetricAsync(string providerCode, bool success, long latencyMs)
    {
        _healthTracker.RecordMetric(providerCode, success, latencyMs);
        return Task.CompletedTask;
    }
}
