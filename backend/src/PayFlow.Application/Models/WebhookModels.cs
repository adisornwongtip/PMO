using PayFlow.Domain.Enums;

namespace PayFlow.Application.Models;

/// <summary>
/// Represents the raw incoming webhook request data received from a payment provider.
/// </summary>
public class WebhookPayload
{
    public string RawBody { get; set; } = string.Empty;
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string? Signature { get; set; }
    public string? EventType { get; set; }

    /// <summary>
    /// Safely gets a header value case-insensitively.
    /// </summary>
    public string? GetHeader(string headerName)
    {
        return Headers.TryGetValue(headerName, out var value) ? value : null;
    }
}

/// <summary>
/// Encapsulates the result of processing a webhook event.
/// </summary>
public class WebhookProcessingResult
{
    public bool Success { get; set; }
    public bool Handled { get; set; }
    public string? Message { get; set; }
    public string? EventType { get; set; }
    public Guid? TransactionId { get; set; }
    public string? ProviderTransactionId { get; set; }
    public PaymentStatus? NewStatus { get; set; }
    public string? Error { get; set; }

    public static WebhookProcessingResult Succeeded(
        Guid? transactionId,
        string? providerTransactionId,
        PaymentStatus status,
        string? eventType = null,
        string? message = null)
    {
        return new WebhookProcessingResult
        {
            Success = true,
            Handled = true,
            TransactionId = transactionId,
            ProviderTransactionId = providerTransactionId,
            NewStatus = status,
            EventType = eventType,
            Message = message ?? "Webhook processed successfully."
        };
    }

    public static WebhookProcessingResult Failed(string error, string? eventType = null)
    {
        return new WebhookProcessingResult
        {
            Success = false,
            Handled = false,
            Error = error,
            EventType = eventType,
            Message = error
        };
    }

    public static WebhookProcessingResult Ignored(string reason, string? eventType = null)
    {
        return new WebhookProcessingResult
        {
            Success = true,
            Handled = false,
            EventType = eventType,
            Message = reason
        };
    }
}
