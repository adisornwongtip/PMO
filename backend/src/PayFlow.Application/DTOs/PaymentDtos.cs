using PayFlow.Domain.Enums;

namespace PayFlow.Application.DTOs;

public class PaymentRequestDto
{
    public string MerchantReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PromptPayQr;
    public string? PaymentToken { get; set; }
    public string? CustomerEmail { get; set; }
    public string? ReturnUrl { get; set; }
}

public class PaymentResponseDto
{
    public Guid TransactionId { get; set; }
    public string MerchantReference { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public string ProviderCode { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string? QrCodeData { get; set; }
    public string? RedirectUrl { get; set; }
    public int AttemptCount { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class StatementRecordDto
{
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string? MerchantReference { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal NetSettlement { get; set; }
    public DateTimeOffset SettlementDate { get; set; }
    public string Status { get; set; } = "SUCCESS";
}

public class ReconciliationResultDto
{
    public Guid BatchId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int DiscrepancyCount { get; set; }
    public decimal TotalSettledAmount { get; set; }
}
