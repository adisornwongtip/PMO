using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Application.Interfaces;
using PayFlow.Infrastructure.Adapters;
using PayFlow.Infrastructure.Persistence;
using PayFlow.Infrastructure.Services;

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

        return services;
    }
}
