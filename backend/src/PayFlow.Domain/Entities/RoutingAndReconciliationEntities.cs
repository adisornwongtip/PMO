using PayFlow.Domain.Enums;

namespace PayFlow.Domain.Entities;

public class RoutingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MerchantId { get; set; } // null = global default
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 1; // 1 = highest priority
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? Currency { get; set; }
    public string PrimaryProvider { get; set; } = string.Empty;
    public string? FallbackProvider { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ReconciliationBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MerchantId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public int TotalRecords { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int DiscrepancyCount { get; set; }
    public decimal TotalSettledAmount { get; set; }
    public List<ReconciliationItem> Items { get; set; } = [];
}

public class ReconciliationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string? MerchantReference { get; set; }
    public Guid? InternalTransactionId { get; set; }
    public decimal StatementAmount { get; set; }
    public decimal? InternalAmount { get; set; }
    public decimal Fee { get; set; }
    public decimal NetSettlement { get; set; }
    public ReconcileStatus Status { get; set; } = ReconcileStatus.Pending;
    public string? DiscrepancyNote { get; set; }
}
