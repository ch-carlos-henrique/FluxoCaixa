using FluxoCaixa.Consolidation.Infrastructure.Telemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FluxoCaixa.Consolidation.API.Extensions;

internal static class ObservabilityExtensions
{
    /// <summary>
    /// Registra OpenTelemetry com traces (ActivitySource) e exporter Console.
    /// O Consolidation service não possui métricas customizadas — apenas tracing distribuído
    /// para correlacionar spans com a Operations API via Correlation ID.
    /// Em produção, substituir AddConsoleExporter() por AddOtlpExporter().
    /// </summary>
    internal static IServiceCollection AddConsolidationObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(
                    serviceName: ConsolidationTelemetry.ServiceName,
                    serviceVersion: ConsolidationTelemetry.ServiceVersion))
            .WithTracing(tracing => tracing
                .AddSource(ConsolidationTelemetry.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
