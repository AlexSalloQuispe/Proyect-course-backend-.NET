using Microsoft.AspNetCore.Http;

namespace UserManagementAPI.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Registrar la excepción completa para diagnóstico, pero devolver un payload de error genérico a los clientes
            _logger.LogError(ex, "Unhandled exception processing request {Method} {Path}", context.Request.Method, context.Request.Path);

            try
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
            }
            catch (Exception writeEx)
            {
                // En el improbable caso de que escribir el error falle, simplemente registrarlo.
                _logger.LogError(writeEx, "Failed to write error response");
            }
        }
    }
}