using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Application.Interfaces;
using PayFlow.Infrastructure.Adapters;
using PayFlow.Infrastructure.Parsers;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Services;
using PayFlow.Infrastructure.Webhooks;

namespace PayFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Use In-Memory database for rapid development/testing, easily switchable to Npgsql in production
        services.AddDbContext<PayFlowDbContext>(options =>
            options.UseInMemoryDatabase("PayFlowDb"));

        services.AddScoped<IPayFlowDbContext>(sp => sp.GetRequiredService<PayFlowDbContext>());

        // Register Provider Adapters
        services.AddSingleton<IPaymentProviderAdapter, MockSandboxAdapter>();
        services.AddSingleton<IPaymentProviderAdapter, OpnAdapter>();
        services.AddSingleton<IPaymentProviderAdapter, GbPrimePayAdapter>();

        // Register Core Engines & Health Tracker
        services.AddSingleton<IProviderHealthTracker, ProviderHealthTracker>();
        services.AddScoped<IRoutingEngine, RoutingEngine>();
        services.AddScoped<IPaymentOrchestrationService, PaymentOrchestrationService>();
        services.AddScoped<IReconciliationService, ReconciliationEngine>();

        // Register Statement Parsers
        services.AddTransient<OpnCsvStatementParser>();
        services.AddTransient<GbPrimePayCsvStatementParser>();
        services.AddTransient<AutoDetectStatementParser>();
        services.AddTransient<IStatementParser, AutoDetectStatementParser>();

        // Register Webhook Processors
        services.AddScoped<IWebhookProcessor, OpnWebhookProcessor>();
        services.AddScoped<IWebhookProcessor, GbPrimePayWebhookProcessor>();

        return services;
    }
}
