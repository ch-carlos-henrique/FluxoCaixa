using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FluxoCaixa.Operations.Infrastructure.Telemetry;

/// <summary>
/// ActivitySource e Meter centralizados para o serviço de Operações.
/// Um ActivitySource e um Meter por serviço é a prática recomendada pelo OpenTelemetry SDK.
/// </summary>
public static class OperationsTelemetry
{
    /// <summary>Nome canônico do serviço — usado pelo OTel SDK e no host builder.</summary>
    public const string ServiceName = "FluxoCaixa.Operations";

    /// <summary>Versão do serviço exposta nas traces e métricas.</summary>
    public const string ServiceVersion = "1.0.0";

    /// <summary>ActivitySource para criação de spans/traces customizados.</summary>
    public static readonly ActivitySource ActivitySource =
        new(ServiceName, ServiceVersion);

    private static readonly Meter _meter = new(ServiceName, ServiceVersion);

    /// <summary>Número de lançamentos (transações) criados com sucesso.</summary>
    public static readonly Counter<long> TransactionsCreated =
        _meter.CreateCounter<long>(
            name: "fluxocaixa.transactions.created",
            unit: "transactions",
            description: "Número de lançamentos criados com sucesso.");

    /// <summary>Número de mensagens do outbox publicadas com sucesso no broker.</summary>
    public static readonly Counter<long> OutboxMessagesPublished =
        _meter.CreateCounter<long>(
            name: "fluxocaixa.outbox.messages.published",
            unit: "messages",
            description: "Número de mensagens do outbox publicadas com sucesso no broker.");

    /// <summary>Número de mensagens do outbox que esgotaram as tentativas (DLQ interno).</summary>
    public static readonly Counter<long> OutboxMessagesFailed =
        _meter.CreateCounter<long>(
            name: "fluxocaixa.outbox.messages.failed",
            unit: "messages",
            description: "Número de mensagens do outbox que esgotaram as tentativas de publicação (DLQ interno).");

    // Campo estático atualizado pelo OutboxPublisherWorker a cada ciclo de polling.
    // volatile garante visibilidade imediata entre threads sem lock overhead.
    private static volatile int _outboxPendingCount;

    /// <summary>Gauge observável: mensagens pendentes no outbox (atualizado a cada ciclo do worker).</summary>
    public static readonly ObservableGauge<int> OutboxMessagesPending;

    static OperationsTelemetry()
    {
        OutboxMessagesPending = _meter.CreateObservableGauge(
            name: "fluxocaixa.outbox.messages.pending",
            observeValue: () => _outboxPendingCount,
            unit: "messages",
            description: "Número de mensagens pendentes na fila outbox.");
    }

    /// <summary>
    /// Atualiza o valor do gauge de mensagens pendentes.
    /// Deve ser chamado pelo OutboxPublisherWorker a cada ciclo de polling.
    /// </summary>
    public static void SetOutboxPendingCount(int count) =>
        _outboxPendingCount = count;
}
