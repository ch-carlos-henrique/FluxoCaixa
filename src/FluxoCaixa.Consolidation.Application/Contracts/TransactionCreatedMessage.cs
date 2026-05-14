namespace FluxoCaixa.Consolidation.Application.Contracts;

/// <summary>
/// Contrato de mensagem consumido pelo serviço de Consolidação quando uma transação é criada.
/// Publicado pelo serviço de Operações via outbox + RabbitMQ.
/// </summary>
public sealed record TransactionCreatedMessage(
    Guid MessageId,
    Guid TransactionId,
    Guid MerchantId,
    string TransactionType,
    decimal Amount,
    string Currency,
    DateTime OccurredAt);
