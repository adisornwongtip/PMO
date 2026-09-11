using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Application.Models;
using PayFlow.Domain.Enums;

namespace PayFlow.Infrastructure.Adapters;

public class MockSandboxAdapter : IPaymentProviderAdapter
{
    public string ProviderCode => "MockSandbox";

    // Chaos simulation flags for testing
    public bool SimulateTimeout { get; set; }
    public bool SimulateServerError { get; set; }
    public bool SimulateDeclined { get; set; }

    public Task<ProviderPaymentResult> ChargeAsync(PaymentRequestDto request, CancellationToken ct = default)
    {
        if (SimulateTimeout)
        {
            return Task.FromResult(new ProviderPaymentResult
            {
                Success = false,
                RawStatusCode = 504,
                ErrorMessage = "Gateway Timeout: Upstream network unreachable",
                LatencyMs = 3000,
                IsRetryable = true
            });
        }

        if (SimulateServerError)
        {
            return Task.FromResult(new ProviderPaymentResult
            {
                Success = false,
                RawStatusCode = 500,
                ErrorMessage = "Internal Server Error from Mock PSP",
                LatencyMs = 250,
                IsRetryable = true
            });
        }

        if (SimulateDeclined)
        {
            return Task.FromResult(new ProviderPaymentResult
            {
                Success = false,
                RawStatusCode = 400,
                ErrorMessage = "Card declined: Insufficient funds",
                LatencyMs = 150,
                IsRetryable = false // Do not failover on client card rejection
            });
        }

        var txnId = $"mock_chrg_{Guid.NewGuid():N}";
        return Task.FromResult(new ProviderPaymentResult
        {
            Success = true,
            ProviderTransactionId = txnId,
            RawStatusCode = 200,
            LatencyMs = 120,
            QrCodeData = request.PaymentMethod == PaymentMethod.PromptPayQr 
                ? $"00020101021229370016A000000677010111011300668123456785802TH5303764540{request.Amount:F2}5802TH6304" 
                : null,
            RedirectUrl = request.PaymentMethod == PaymentMethod.CreditCard
                ? $"https://sandbox.payflow.io/mock/3ds/{txnId}"
                : null,
            IsRetryable = false
        });
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        return Task.FromResult(!SimulateServerError && !SimulateTimeout);
    }
}

public class OpnAdapter : IPaymentProviderAdapter
{
    public string ProviderCode => "Opn";
    public bool SimulateDowntime { get; set; }

    public Task<ProviderPaymentResult> ChargeAsync(PaymentRequestDto request, CancellationToken ct = default)
    {
        if (SimulateDowntime)
        {
            return Task.FromResult(new ProviderPaymentResult
            {
                Success = false,
                RawStatusCode = 503,
                ErrorMessage = "Opn Gateway Maintenance",
                LatencyMs = 1500,
                IsRetryable = true
            });
        }

        var opnChargeId = $"chrg_opn_{Guid.NewGuid().ToString("N")[..16]}";
        return Task.FromResult(new ProviderPaymentResult
        {
            Success = true,
            ProviderTransactionId = opnChargeId,
            RawStatusCode = 200,
            LatencyMs = 210,
            QrCodeData = request.PaymentMethod == PaymentMethod.PromptPayQr
                ? $"00020101021229370016A000000677010111011300668999999995802TH5303764540{request.Amount:F2}5802TH6304"
                : null,
            RedirectUrl = $"https://api.omise.co/charges/{opnChargeId}/authorize",
            IsRetryable = false
        });
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        return Task.FromResult(!SimulateDowntime);
    }
}

public class GbPrimePayAdapter : IPaymentProviderAdapter
{
    public string ProviderCode => "GBPrimePay";
    public bool SimulateDowntime { get; set; }

    public Task<ProviderPaymentResult> ChargeAsync(PaymentRequestDto request, CancellationToken ct = default)
    {
        if (SimulateDowntime)
        {
            return Task.FromResult(new ProviderPaymentResult
            {
                Success = false,
                RawStatusCode = 502,
                ErrorMessage = "GB Prime Pay Bad Gateway",
                LatencyMs = 2000,
                IsRetryable = true
            });
        }

        var gbTxnId = $"GB_{DateTime.UtcNow:yyyyMMddHHmmss}_{Random.Shared.Next(1000, 9999)}";
        return Task.FromResult(new ProviderPaymentResult
        {
            Success = true,
            ProviderTransactionId = gbTxnId,
            RawStatusCode = 200,
            LatencyMs = 180,
            QrCodeData = request.PaymentMethod == PaymentMethod.PromptPayQr
                ? $"00020101021229370016A000000677010111011300668888888885802TH5303764540{request.Amount:F2}5802TH6304"
                : null,
            RedirectUrl = $"https://api.gbprimepay.com/v3/paygate/{gbTxnId}",
            IsRetryable = false
        });
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        return Task.FromResult(!SimulateDowntime);
    }
}
