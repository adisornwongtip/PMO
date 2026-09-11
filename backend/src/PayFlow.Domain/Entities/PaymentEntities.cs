using PayFlow.Domain.Enums;

namespace PayFlow.Domain.Entities;

public class Merchant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class RoutingAttemptLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentTransactionId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public long LatencyMs { get; set; }
}

public class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MerchantId { get; set; }
    public string MerchantReference { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string SelectedProvider { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public int AttemptsCount { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<RoutingAttemptLog> RoutingLogs { get; set; } = [];
}
