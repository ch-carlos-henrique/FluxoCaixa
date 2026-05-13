namespace FluxoCaixa.Consolidation.Application.Contracts;

/// <summary>
/// Contrato de mensagem publicado pelo serviço de Operações quando uma transação é criada.
/// Consumido pelo serviço de Consolidação para atualizar o saldo diário.
/// </summary>
public sealed record TransactionCreatedMessage(
    Guid MessageId,
    Guid TransactionId,
    Guid MerchantId,
    string TransactionType,
    decimal Amount,
    DateTime OccurredAt);
