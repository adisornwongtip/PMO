using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;

namespace PayFlow.Infrastructure.Middleware;

/// <summary>
/// Middleware to authenticate incoming requests via 'X-API-Key' or 'Authorization: Bearer &lt;key&gt;'.
/// Validates the API key against registered, active merchants and sets security context.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    // Endpoints that bypass API key authentication (e.g., webhook receivers, OpenAPI documentation, health endpoints)
    private static readonly string[] PublicPathPrefixes =
    [
        "/openapi",
        "/swagger",
        "/scalar",
        "/health",
        "/healthz",
        "/api/v1/webhooks"
    ];

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IPayFlowDbContext dbContext)
    {
        var path = context.Request.Path;

        // 1. Bypass public paths
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // 2. Extract API Key from X-API-Key or Authorization header
        var apiKey = ExtractApiKey(context.Request);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Missing API key. Provide 'X-API-Key' or 'Authorization: Bearer <key>'.\"}");
            return;
        }

        // 3. Validate merchant against database
        var merchant = await dbContext.Merchants
            .AsNoTracking()
            .FirstOrDefaultAsync(m => (m.ApiKey == apiKey || m.ApiSecret == apiKey) && m.IsActive);

        if (merchant == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Invalid API key.\"}");
            return;
        }

        // 4. Set merchant context on HttpContext and User Claims
        context.Items["Merchant"] = merchant;
        context.Items["MerchantId"] = merchant.Id;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, merchant.Id.ToString()),
            new Claim(ClaimTypes.Name, merchant.Name),
            new Claim("MerchantId", merchant.Id.ToString()),
            new Claim("ApiKey", merchant.ApiKey)
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);

        await _next(context);
    }

    private static bool IsPublicPath(PathString path)
    {
        if (path == "/" || string.IsNullOrEmpty(path.Value))
        {
            return true;
        }

        foreach (var prefix in PublicPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        // Check X-API-Key header
        if (request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader) && !string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return apiKeyHeader.ToString().Trim();
        }

        // Check Authorization header
        if (request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
        {
            var headerValue = authHeader.ToString().Trim();
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return headerValue["Bearer ".Length..].Trim();
            }

            return headerValue;
        }

        return null;
    }
}

public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
