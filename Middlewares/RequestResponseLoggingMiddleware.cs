using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace UserManagementAPI.Middlewares;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userName = context.User?.Identity?.IsAuthenticated == true ? context.User.Identity?.Name : "anonymous";

        _logger.LogInformation("Incoming request {Method} {Path} from {RemoteIp} as {User} ({CorrelationId})", context.Request.Method, context.Request.Path + context.Request.QueryString, remoteIp, userName, correlationId);

        // Registrar un callback para registrar la respuesta saliente cuando se empiecen a enviar las cabeceras.
        var logged = false;
        context.Response.OnStarting(() =>
        {
            if (!logged)
            {
                sw.Stop();
                _logger.LogInformation("Outgoing response {StatusCode} for {Method} {Path} from {RemoteIp} as {User} ({CorrelationId}) completed in {Elapsed}ms", context.Response.StatusCode, context.Request.Method, context.Request.Path + context.Request.QueryString, remoteIp, userName, correlationId, sw.ElapsedMilliseconds);
                logged = true;
            }
            return Task.CompletedTask;
        });

        try
        {
            await _next(context);
        }
        catch
        {
            // Volver a lanzar la excepción para que la maneje el manejador global; OnStarting se ejecutará cuando el manejador escriba la respuesta.
            throw;
        }
        finally
        {
            // Asegurar que registramos si OnStarting no se ejecutó por alguna razón (por ejemplo, la respuesta nunca se inició).
            if (!logged)
            {
                sw.Stop();
                _logger.LogInformation("Outgoing response {StatusCode} for {Method} {Path} from {RemoteIp} as {User} ({CorrelationId}) completed in {Elapsed}ms", context.Response.StatusCode, context.Request.Method, context.Request.Path + context.Request.QueryString, remoteIp, userName, correlationId, sw.ElapsedMilliseconds);
            }
        }
    }
}