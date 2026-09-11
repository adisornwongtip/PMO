namespace PayFlow.Application.Models;

public class ProviderPaymentResult
{
    public bool Success { get; set; }
    public string? ProviderTransactionId { get; set; }
    public int RawStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public long LatencyMs { get; set; }
    public string? QrCodeData { get; set; }
    public string? RedirectUrl { get; set; }
    public bool IsRetryable { get; set; }
}

public class RoutingDecision
{
    public string PrimaryProvider { get; set; } = string.Empty;
    public string? FallbackProvider { get; set; }
    public string MatchedRuleName { get; set; } = "Default";
}
