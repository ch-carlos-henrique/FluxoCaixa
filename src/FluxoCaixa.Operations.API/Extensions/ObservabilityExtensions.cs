using FluxoCaixa.Operations.Infrastructure.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FluxoCaixa.Operations.API.Extensions;

internal static class ObservabilityExtensions
{
    /// <summary>
    /// Registra OpenTelemetry com traces (ActivitySource), métricas customizadas (Meter) e
    /// exporter Console — adequado para desenvolvimento local sem coletor externo.
    /// Em produção, substituir AddConsoleExporter() por AddOtlpExporter() apontando para
    /// Jaeger, Grafana Tempo ou Azure Monitor.
    /// </summary>
    internal static IServiceCollection AddOperationsObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(
                    serviceName: OperationsTelemetry.ServiceName,
                    serviceVersion: OperationsTelemetry.ServiceVersion))
            .WithTracing(tracing => tracing
                .AddSource(OperationsTelemetry.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(OperationsTelemetry.ServiceName)
                .AddConsoleExporter());

        return services;
    }
}
