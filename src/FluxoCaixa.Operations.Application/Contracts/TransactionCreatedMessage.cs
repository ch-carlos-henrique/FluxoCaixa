namespace FluxoCaixa.Operations.Application.Contracts;

/// <summary>
/// Contrato de mensagem publicado pelo serviço de Operações ao criar uma transação.
/// Armazenado no outbox e publicado para o RabbitMQ pelo OutboxPublisherWorker.
/// </summary>
public sealed record TransactionCreatedMessage(
    Guid MessageId,
    Guid TransactionId,
    Guid MerchantId,
    string TransactionType,
    decimal Amount,
    string Currency,
    DateTime OccurredAt);
