namespace FluxoCaixa.Consolidation.Infrastructure.Persistence;

/// <summary>
/// Registro de mensagem já processada pelo consumer do Consolidado.
/// Garante idempotência: cada MessageId é processado exatamente uma vez.
/// </summary>
internal sealed class ProcessedMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// ID único da mensagem MassTransit (campo MessageId do contrato).
    /// </summary>
    public Guid EventId { get; set; }

    public DateTime ProcessedAt { get; set; }
}
