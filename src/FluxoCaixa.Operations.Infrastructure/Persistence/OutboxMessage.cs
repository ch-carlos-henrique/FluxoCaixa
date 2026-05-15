namespace FluxoCaixa.Operations.Infrastructure.Persistence;

internal sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Payload JSON do evento serializado (TransactionCreatedMessage).
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Status da mensagem: "Pending", "Processing", "Published" ou "Failed".
    /// "Processing": worker reivindicou a mensagem — evita duplo processamento em múltiplas réplicas.
    /// </summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Preenchido quando status = "Processing". Permite recuperação de mensagens travadas
    /// por réplicas que falharam antes de publicar.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    public int RetryCount { get; set; }
}
