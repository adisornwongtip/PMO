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
/// Webhook processor for Opn (formerly Omise) payment events with HMAC SHA-256 signature verification.
/// </summary>
public class OpnWebhookProcessor : IWebhookProcessor
{
    private readonly IPayFlowDbContext _dbContext;

    public string ProviderCode => "Opn";

    public OpnWebhookProcessor(IPayFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Validates HMAC SHA-256 signature against Opn payload.
    /// Checks payload.Signature or X-Opn-Signature / X-Omise-Signature headers.
    /// </summary>
    public bool ValidateSignature(WebhookPayload payload, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(payload.RawBody))
        {
            return false;
        }

        var signature = payload.Signature
            ?? payload.GetHeader("X-Opn-Signature")
            ?? payload.GetHeader("X-Omise-Signature")
            ?? payload.GetHeader("Omise-Signature");

        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        if (signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            signature = signature[7..];
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload.RawBody));

        // Compare lowercase/uppercase hex
        var hexSignature = Convert.ToHexString(hashBytes);
        if (string.Equals(signature, hexSignature, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Compare base64
        var base64Signature = Convert.ToBase64String(hashBytes);
        if (string.Equals(signature, base64Signature, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses Opn webhook JSON, processes charge.complete events, and transitions transaction status.
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

        // 1. Verify HMAC SHA-256 signature if secretKey provided
        if (!string.IsNullOrEmpty(secretKey) && !ValidateSignature(payload, secretKey))
        {
            return WebhookProcessingResult.Failed("Invalid Opn webhook HMAC signature.");
        }

        // 2. Parse JSON
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(payload.RawBody);
        }
        catch (JsonException ex)
        {
            return WebhookProcessingResult.Failed($"Invalid JSON format: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Extract event type / key
            string? eventKey = null;
            if (root.TryGetProperty("key", out var keyProp))
            {
                eventKey = keyProp.GetString();
            }
            else if (root.TryGetProperty("event", out var evProp))
            {
                eventKey = evProp.GetString();
            }
            else if (root.TryGetProperty("type", out var tpProp))
            {
                eventKey = tpProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(eventKey))
            {
                return WebhookProcessingResult.Failed("Missing event key or type in Opn webhook payload.");
            }

            // We handle charge.complete and charge.failed
            if (!eventKey.Equals("charge.complete", StringComparison.OrdinalIgnoreCase) &&
                !eventKey.Equals("charge.failed", StringComparison.OrdinalIgnoreCase))
            {
                return WebhookProcessingResult.Ignored($"Opn event '{eventKey}' ignored.", eventKey);
            }

            if (!root.TryGetProperty("data", out var dataProp))
            {
                return WebhookProcessingResult.Failed("Missing 'data' element in Opn webhook payload.", eventKey);
            }

            var chargeId = dataProp.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var status = dataProp.TryGetProperty("status", out var stProp) ? stProp.GetString() : null;
            var isPaid = dataProp.TryGetProperty("paid", out var paidProp) && paidProp.GetBoolean();

            // Locate PaymentTransaction
            PaymentTransaction? txn = null;
            if (!string.IsNullOrEmpty(chargeId))
            {
                txn = await _dbContext.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.ProviderTransactionId == chargeId, ct);
            }

            // Fallback: look up by metadata if not found by chargeId
            if (txn == null && dataProp.TryGetProperty("metadata", out var metaProp))
            {
                if (metaProp.TryGetProperty("transactionId", out var tidProp) && Guid.TryParse(tidProp.GetString(), out var tid))
                {
                    txn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == tid, ct);
                }
                else if (metaProp.TryGetProperty("merchantReference", out var mrProp))
                {
                    var mr = mrProp.GetString();
                    if (!string.IsNullOrEmpty(mr))
                    {
                        txn = await _dbContext.PaymentTransactions.FirstOrDefaultAsync(t => t.MerchantReference == mr, ct);
                    }
                }
            }

            if (txn == null)
            {
                return WebhookProcessingResult.Failed(
                    $"Payment transaction not found for Opn charge ID '{chargeId}'.",
                    eventKey);
            }

            // Validate signature against merchant's webhook secret if not already validated
            if (string.IsNullOrEmpty(secretKey))
            {
                var merchant = await _dbContext.Merchants.FirstOrDefaultAsync(m => m.Id == txn.MerchantId, ct);
                var merchantSecret = merchant?.WebhookSecret ?? merchant?.ApiSecret;
                if (!string.IsNullOrEmpty(merchantSecret) &&
                    (payload.Signature != null || payload.GetHeader("X-Opn-Signature") != null || payload.GetHeader("X-Omise-Signature") != null))
                {
                    if (!ValidateSignature(payload, merchantSecret))
                    {
                        return WebhookProcessingResult.Failed("Invalid Opn webhook HMAC signature for merchant.", eventKey);
                    }
                }
            }

            // Idempotent check: if already Success, return immediately
            if (txn.Status == PaymentStatus.Success)
            {
                return WebhookProcessingResult.Succeeded(
                    txn.Id,
                    txn.ProviderTransactionId,
                    txn.Status,
                    eventKey,
                    "Transaction is already in Success state (idempotent).");
            }

            // Process event outcome
            if (eventKey.Equals("charge.complete", StringComparison.OrdinalIgnoreCase) &&
                (status == null || status.Equals("successful", StringComparison.OrdinalIgnoreCase) || isPaid))
            {
                txn.Status = PaymentStatus.Success;
                txn.CompletedAt = DateTimeOffset.UtcNow;
                if (string.IsNullOrEmpty(txn.ProviderTransactionId) && !string.IsNullOrEmpty(chargeId))
                {
                    txn.ProviderTransactionId = chargeId;
                }

                await _dbContext.SaveChangesAsync(ct);

                return WebhookProcessingResult.Succeeded(
                    txn.Id,
                    txn.ProviderTransactionId,
                    PaymentStatus.Success,
                    eventKey,
                    "Opn charge completed successfully.");
            }
            else
            {
                txn.Status = PaymentStatus.Failed;
                txn.CompletedAt = DateTimeOffset.UtcNow;
                txn.FailureReason = dataProp.TryGetProperty("failure_message", out var fm)
                    ? fm.GetString()
                    : $"Opn charge status: {status ?? "failed"}";

                await _dbContext.SaveChangesAsync(ct);

                return WebhookProcessingResult.Succeeded(
                    txn.Id,
                    txn.ProviderTransactionId,
                    PaymentStatus.Failed,
                    eventKey,
                    txn.FailureReason);
            }
        }
    }
}
