using System.Text.Json;

namespace FluxoCaixa.Operations.API.Middleware;

internal sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate                        _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next   = next;
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
            _logger.LogError(ex, "Erro não tratado em {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception _)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = StatusCodes.Status500InternalServerError;

        var body = JsonSerializer.Serialize(new
        {
            status        = 500,
            error         = "Erro interno do servidor.",
            correlationId = context.Items["X-Correlation-ID"]?.ToString()
        });

        await context.Response.WriteAsync(body);
    }
}
