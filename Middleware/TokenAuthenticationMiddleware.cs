using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UserManagementAPI.Middleware;

public sealed class TokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly HashSet<string> _validTokens;

    public TokenAuthenticationMiddleware(RequestDelegate next, ILogger<TokenAuthenticationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        var tokens = configuration.GetSection("ApiSecurity:ValidTokens").Get<string[]>() ?? Array.Empty<string>();
        _validTokens = new HashSet<string>(tokens, StringComparer.Ordinal);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSwaggerRequest(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            await ReturnUnauthorized(context, "Authorization header is required.");
            return;
        }

        var token = ExtractBearerToken(authHeader.ToString());
        if (string.IsNullOrWhiteSpace(token) || !_validTokens.Contains(token))
        {
            await ReturnUnauthorized(context, "Invalid or missing token.");
            return;
        }

        await _next(context);
    }

    private static bool IsSwaggerRequest(PathString path)
    {
        return path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/swagger-ui", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/swagger/v1/swagger.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractBearerToken(string authHeader)
    {
        const string bearerPrefix = "Bearer ";
        if (!authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authHeader[bearerPrefix.Length..].Trim();
    }

    private static async Task ReturnUnauthorized(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
