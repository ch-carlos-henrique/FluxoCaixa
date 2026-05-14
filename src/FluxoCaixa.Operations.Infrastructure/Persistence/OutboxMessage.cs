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
    /// Status da mensagem: "Pending", "Published" ou "Failed".
    /// </summary>
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int RetryCount { get; set; }
}
