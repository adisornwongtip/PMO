using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Domain.Enums;
using PayFlow.Infrastructure;
using PayFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add Infrastructure dependencies
builder.Services.AddPayFlowInfrastructure(builder.Configuration);

// Add Native .NET 10 OpenAPI
builder.Services.AddOpenApi();

// Add CORS for frontend integration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");
app.MapOpenApi();

// Seed default demo merchant & routing rules
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PayFlowDbContext>();
    db.Database.EnsureCreated();

    var demoMerchantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    if (!db.Merchants.Any(m => m.Id == demoMerchantId))
    {
        db.Merchants.Add(new Merchant
        {
            Id = demoMerchantId,
            Name = "Demo Enterprise Merchant",
            ApiKey = "pk_live_payflow_demo_9824",
            ApiSecret = "sk_live_payflow_secret_demo",
            WebhookUrl = "https://merchant.example.com/webhook",
            IsActive = true
        });

        // Seed default smart routing rules
        db.RoutingRules.AddRange(
            new RoutingRule
            {
                Name = "PromptPay High Volume QR",
                Priority = 1,
                PaymentMethod = PaymentMethod.PromptPayQr,
                PrimaryProvider = "Opn",
                FallbackProvider = "GBPrimePay",
                IsEnabled = true
            },
            new RoutingRule
            {
                Name = "Credit Card Tier 1 Routing",
                Priority = 2,
                PaymentMethod = PaymentMethod.CreditCard,
                MinAmount = 100,
                MaxAmount = 50000,
                PrimaryProvider = "Opn",
                FallbackProvider = "MockSandbox",
                IsEnabled = true
            },
            new RoutingRule
            {
                Name = "Large Transactions Safety Rule",
                Priority = 3,
                MinAmount = 50000,
                PrimaryProvider = "GBPrimePay",
                FallbackProvider = "Opn",
                IsEnabled = true
            }
        );

        db.SaveChanges();
    }
}

// ---------------------------------------------------------
// PAYMENTS API
// ---------------------------------------------------------

app.MapPost("/api/v1/payments", async (
    [FromHeader(Name = "X-Merchant-Id")] string? merchantIdHeader,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
    [FromBody] PaymentRequestDto request,
    [FromServices] IPaymentOrchestrationService orchestrationService) =>
{
    var merchantId = Guid.TryParse(merchantIdHeader, out var parsed) 
        ? parsed 
        : Guid.Parse("11111111-1111-1111-1111-111111111111");

    var idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKeyHeader)
        ? $"idemp_{Guid.NewGuid():N}"
        : idempotencyKeyHeader;

    var result = await orchestrationService.ProcessPaymentAsync(merchantId, idempotencyKey, request);
    return Results.Ok(result);
})
.WithName("ProcessPayment")
.WithSummary("Process a payment through Smart Routing and Auto-Failover");

app.MapGet("/api/v1/payments", async (
    [FromServices] IPayFlowDbContext db,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20) =>
{
    var txns = await db.PaymentTransactions
        .Include(t => t.RoutingLogs)
        .OrderByDescending(t => t.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Results.Ok(txns);
})
.WithName("GetTransactions");

app.MapGet("/api/v1/payments/{id:guid}", async (
    Guid id,
    [FromServices] IPayFlowDbContext db) =>
{
    var txn = await db.PaymentTransactions
        .Include(t => t.RoutingLogs)
        .FirstOrDefaultAsync(t => t.Id == id);

    return txn is not null ? Results.Ok(txn) : Results.NotFound();
})
.WithName("GetTransactionById");

// ---------------------------------------------------------
// RECONCILIATION API
// ---------------------------------------------------------

app.MapPost("/api/v1/reconciliation/upload", async (
    [FromHeader(Name = "X-Merchant-Id")] string? merchantIdHeader,
    [FromBody] ReconcileUploadRequest request,
    [FromServices] IReconciliationService reconcileService) =>
{
    var merchantId = Guid.TryParse(merchantIdHeader, out var parsed) 
        ? parsed 
        : Guid.Parse("11111111-1111-1111-1111-111111111111");

    var batch = await reconcileService.ProcessStatementBatchAsync(
        merchantId,
        request.ProviderCode,
        request.FileName,
        request.Records);

    return Results.Ok(new ReconciliationResultDto
    {
        BatchId = batch.Id,
        ProviderCode = batch.ProviderCode,
        FileName = batch.FileName,
        TotalRecords = batch.TotalRecords,
        MatchedCount = batch.MatchedCount,
        UnmatchedCount = batch.UnmatchedCount,
        DiscrepancyCount = batch.DiscrepancyCount,
        TotalSettledAmount = batch.TotalSettledAmount
    });
})
.WithName("UploadReconciliationBatch");

app.MapGet("/api/v1/reconciliation/batches", async (
    [FromServices] IPayFlowDbContext db) =>
{
    var batches = await db.ReconciliationBatches
        .Include(b => b.Items)
        .OrderByDescending(b => b.UploadedAt)
        .ToListAsync();

    return Results.Ok(batches);
})
.WithName("GetReconciliationBatches");

// ---------------------------------------------------------
// ROUTING RULES API
// ---------------------------------------------------------

app.MapGet("/api/v1/routing-rules", async (
    [FromServices] IPayFlowDbContext db) =>
{
    var rules = await db.RoutingRules
        .OrderBy(r => r.Priority)
        .ToListAsync();

    return Results.Ok(rules);
})
.WithName("GetRoutingRules");

app.MapPost("/api/v1/routing-rules", async (
    [FromBody] RoutingRule rule,
    [FromServices] IPayFlowDbContext db) =>
{
    db.RoutingRules.Add(rule);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/routing-rules/{rule.Id}", rule);
})
.WithName("CreateRoutingRule");

// ---------------------------------------------------------
// DASHBOARD METRICS API
// ---------------------------------------------------------

app.MapGet("/api/v1/dashboard/metrics", async (
    [FromServices] IPayFlowDbContext db) =>
{
    var txns = await db.PaymentTransactions.ToListAsync();
    var totalCount = txns.Count;
    var successCount = txns.Count(t => t.Status == PaymentStatus.Success);
    var failCount = txns.Count(t => t.Status == PaymentStatus.Failed);
    var gmv = txns.Where(t => t.Status == PaymentStatus.Success).Sum(t => t.Amount);

    var successRate = totalCount > 0 ? (double)successCount / totalCount * 100 : 100.0;

    var providerStats = txns.GroupBy(t => t.SelectedProvider)
        .Select(g => new
        {
            Provider = g.Key,
            Count = g.Count(),
            SuccessCount = g.Count(x => x.Status == PaymentStatus.Success),
            SuccessRate = g.Count() > 0 ? (double)g.Count(x => x.Status == PaymentStatus.Success) / g.Count() * 100 : 0
        }).ToList();

    return Results.Ok(new
    {
        GMV = gmv,
        TotalTransactions = totalCount,
        SuccessCount = successCount,
        FailedCount = failCount,
        SuccessRate = Math.Round(successRate, 2),
        EstimatedFeesSaved = Math.Round(gmv * 0.0035m, 2), // Estimation based on 0.35% optimization
        Providers = providerStats
    });
})
.WithName("GetDashboardMetrics");

app.Run();

public record ReconcileUploadRequest(string ProviderCode, string FileName, List<StatementRecordDto> Records);
