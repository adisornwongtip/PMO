using PayFlow.Application.Models;

namespace PayFlow.Application.Interfaces;

/// <summary>
/// Defines the contract for provider-specific webhook processing and HMAC SHA-256 verification.
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// The unique code identifying the payment provider (e.g., "Opn", "GBPrimePay").
    /// </summary>
    string ProviderCode { get; }

    /// <summary>
    /// Validates the HMAC SHA-256 signature or checksum of the incoming webhook payload against the secret key.
    /// </summary>
    /// <param name="payload">Incoming webhook payload containing raw body and headers.</param>
    /// <param name="secretKey">Provider webhook signing secret or merchant secret key.</param>
    /// <returns>True if signature matches; otherwise false.</returns>
    bool ValidateSignature(WebhookPayload payload, string secretKey);

    /// <summary>
    /// Parses the webhook payload, updates the transaction status, and returns a structured processing result.
    /// </summary>
    /// <param name="payload">Incoming webhook payload containing raw body and headers.</param>
    /// <param name="secretKey">Optional webhook signing secret for signature verification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Processing outcome with updated transaction details.</returns>
    Task<WebhookProcessingResult> ProcessWebhookAsync(
        WebhookPayload payload,
        string? secretKey = null,
        CancellationToken ct = default);
}
