using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UserManagementAPI.Middlewares;

public class TokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly string? _apiKey;

    public TokenAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<TokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _apiKey = configuration["Auth:ApiKey"]; // configurada en appsettings.json

        if (string.IsNullOrEmpty(_apiKey))
            _logger.LogWarning("No API key configured (Auth:ApiKey). In production this should be set to protect endpoints.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Proteger solo rutas de la API
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                // sin clave configurada -> permitir en desarrollo local pero registrar advertencia
                await _next(context);
                return;
            }

            string? token = null;
            if (context.Request.Headers.TryGetValue("Authorization", out var auth) && auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = auth.ToString().Substring("Bearer ".Length).Trim();
            }
            else if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
            {
                token = apiKeyHeader.ToString();
            }

            if (string.IsNullOrEmpty(token) || !string.Equals(token, _apiKey, StringComparison.Ordinal))
            {
                _logger.LogWarning("Unauthorized request to {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new { title = "Unauthorized", status = 401, detail = "Missing or invalid authentication token." });
                return;
            }

            // establecer un ClaimsPrincipal mínimo
            var claims = new[] { new Claim(ClaimTypes.Name, "api-client"), new Claim("scope", "api") };
            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }
}