using Serilog.Context;

namespace FluxoCaixa.Operations.API.Middleware;

internal sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        var correlationIdValue = correlationId.ToString();

        context.Items[CorrelationIdHeader]            = correlationIdValue;
        context.Response.Headers[CorrelationIdHeader] = correlationIdValue;

        // Propaga o CorrelationId para todos os logs Serilog gerados durante a requisição.
        using (LogContext.PushProperty("CorrelationId", correlationIdValue))
        {
            await _next(context);
        }
    }
}
