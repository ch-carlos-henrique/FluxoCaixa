namespace FluxoCaixa.Operations.Application.Commands;

public sealed record CreateTransactionCommand(
    Guid MerchantId,
    string Type,
    decimal Amount,
    string Currency,
    string? Description,
    DateTime OccurredAt,
    string IdempotencyKey);
