using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;
using PayFlow.Application.Models;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;

namespace PayFlow.Infrastructure.Webhooks;

/// <summary>
/// Webhook processor for GB Prime Pay payment events with HMAC SHA-256 / checksum verification.
/// </summary>
public class GbPrimePayWebhookProcessor : IWebhookProcessor
{
    private readonly IPayFlowDbContext _dbContext;

    public string ProviderCode => "GBPrimePay";

    public GbPrimePayWebhookProcessor(IPayFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Validates GB Prime Pay checksum/signature against the secret key.
    /// Checks payload.Signature, headers (X-GB-Signature, X-Checksum), or payload body 'checksum' field.
    /// </summary>
    public bool ValidateSignature(WebhookPayload payload, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(payload.RawBody))
        {
            return false;
        }

        var signature = payload.Signature
            ?? payload.GetHeader("X-GB-Signature")
            ?? payload.GetHeader("X-Checksum")
            ?? payload.GetHeader("X-Signature");

        // If not in headers or payload.Signature, check if JSON contains 'checksum' or 'signature'
        if (string.IsNullOrWhiteSpace(signature) && payload.RawBody.TrimStart().StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload.RawBody);
                if (doc.RootElement.TryGetProperty("checksum", out var csProp))
                {
                    signature = csProp.GetString();
                }
                else if (doc.RootElement.TryGetProperty("signature", out var sigProp))
                {
                    signature = sigProp.GetString();
                }
            }
            catch
            {
                // Fall through if not JSON
            }
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        if (signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            signature = signature[7..];
        }

        // Direct token comparison (some GB Prime Pay configurations use a static secret token check)
        if (string.Equals(signature, secretKey, StringComparison.Ordinal))
        {
            return true;
        }

        // HMAC-SHA256 of the raw body
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload.RawBody));

        var hexSignature = Convert.ToHexString(hashBytes);
        if (string.Equals(signature, hexSignature, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var base64Signature = Convert.ToBase64String(hashBytes);
        if (string.Equals(signature, base64Signature, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses GB Prime Pay webhook, validates checksum/secret, and updates transaction status.
    /// </summary>
    public async Task<WebhookProcessingResult> ProcessWebhookAsync(
        WebhookPayload payload,
        string? secretKey = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payload.RawBody))
        {
            return WebhookProcessingResult.Failed("Empty webhook payload body.");
        }

        // 1. Verify HMAC / Checksum if secretKey provided
        if (!string.IsNullOrEmpty(secretKey) && !ValidateSignature(payload, secretKey))
        {
            return WebhookProcessingResult.Failed("Invalid GBPrimePay webhook signature/checksum.");
        }

        // 2. Parse JSON
        string? referenceNo = null;
        string? gbpReferenceNo = null;
        string? resultCode = null;
        string? resultMessage = null;
        string? status = null;

        try
        {
            using var doc = JsonDocument.Parse(payload.RawBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("referenceNo", out var rProp))
            {
                referenceNo = rProp.GetString();
            }

            if (root.TryGetProperty("gbpReferenceNo", out var gProp))
            {
                gbpReferenceNo = gProp.GetString();
            }

            if (root.TryGetProperty("resultCode", out var rcProp))
            {
                resultCode = rcProp.GetString();
            }

            if (root.TryGetProperty("resultMessage", out var rmProp))
            {
                resultMessage = rmProp.GetString();
            }
            else if (root.TryGetProperty("detail", out var dProp))
            {
                resultMessage = dProp.GetString();
            }

            if (root.TryGetProperty("status", out var sProp))
            {
                status = sProp.GetString();
            }
        }
        catch (JsonException ex)
        {
            return WebhookProcessingResult.Failed($"Invalid GBPrimePay JSON: {ex.Message}");
        }

        // 3. Locate transaction
        PaymentTransaction? txn = null;

        if (!string.IsNullOrEmpty(gbpReferenceNo))
        {
            txn = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(t => t.ProviderTransactionId == gbpReferenceNo, ct);
        }

        if (txn == null && !string.IsNullOrEmpty(referenceNo))
        {
            if (Guid.TryParse(referenceNo, out var tid))
            {
                txn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == tid, ct);
            }

            if (txn == null)
            {
                txn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.MerchantReference == referenceNo, ct);
            }
        }

        if (txn == null)
        {
            return WebhookProcessingResult.Failed(
                $"Payment transaction not found for GBPrimePay reference '{referenceNo}' / '{gbpReferenceNo}'.",
                "gbprimepay.callback");
        }

        // Validate signature against merchant's webhook secret if not already validated
        if (string.IsNullOrEmpty(secretKey))
        {
            var merchant = await _dbContext.Merchants.FirstOrDefaultAsync(m => m.Id == txn.MerchantId, ct);
            var merchantSecret = merchant?.WebhookSecret ?? merchant?.ApiSecret;
            if (!string.IsNullOrEmpty(merchantSecret) &&
                (payload.Signature != null || payload.GetHeader("X-GB-Signature") != null || payload.GetHeader("X-Checksum") != null))
            {
                if (!ValidateSignature(payload, merchantSecret))
                {
                    return WebhookProcessingResult.Failed("Invalid GBPrimePay webhook signature/checksum for merchant.", "gbprimepay.callback");
                }
            }
        }

        // Idempotency: if already Success, return immediately
        if (txn.Status == PaymentStatus.Success)
        {
            return WebhookProcessingResult.Succeeded(
                txn.Id,
                txn.ProviderTransactionId,
                txn.Status,
                "gbprimepay.callback",
                "Transaction already marked as Success (idempotent).");
        }

        // resultCode "00" or status "S" / "SUCCESS" represents successful transaction in GB Prime Pay
        var isSuccess = (resultCode == "00") ||
                        string.Equals(status, "S", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase);

        if (isSuccess)
        {
            txn.Status = PaymentStatus.Success;
            txn.CompletedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(gbpReferenceNo))
            {
                txn.ProviderTransactionId = gbpReferenceNo;
            }

            await _dbContext.SaveChangesAsync(ct);

            return WebhookProcessingResult.Succeeded(
                txn.Id,
                txn.ProviderTransactionId,
                PaymentStatus.Success,
                "gbprimepay.callback",
                "GBPrimePay payment completed successfully.");
        }
        else
        {
            txn.Status = PaymentStatus.Failed;
            txn.CompletedAt = DateTimeOffset.UtcNow;
            txn.FailureReason = resultMessage ?? $"GBPrimePay returned error code {resultCode ?? "UNKNOWN"}";

            await _dbContext.SaveChangesAsync(ct);

            return WebhookProcessingResult.Succeeded(
                txn.Id,
                txn.ProviderTransactionId,
                PaymentStatus.Failed,
                "gbprimepay.callback",
                txn.FailureReason);
        }
    }
}
