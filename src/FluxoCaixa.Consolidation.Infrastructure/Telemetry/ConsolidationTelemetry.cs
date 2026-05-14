using System.Diagnostics;

namespace FluxoCaixa.Consolidation.Infrastructure.Telemetry;

/// <summary>
/// ActivitySource centralizado para o serviço de Consolidação.
/// Um ActivitySource por serviço é a prática recomendada pelo OpenTelemetry SDK.
/// </summary>
public static class ConsolidationTelemetry
{
    /// <summary>Nome canônico do serviço — usado pelo OTel SDK e no host builder.</summary>
    public const string ServiceName = "FluxoCaixa.Consolidation";

    /// <summary>Versão do serviço exposta nas traces.</summary>
    public const string ServiceVersion = "1.0.0";

    /// <summary>ActivitySource para criação de spans/traces customizados.</summary>
    public static readonly ActivitySource ActivitySource =
        new(ServiceName, ServiceVersion);
}
