namespace FluxoCaixa.Operations.Application.Dtos;

public sealed record TransactionDto(
    Guid Id,
    Guid MerchantId,
    string Type,
    decimal Amount,
    string Currency,
    string? Description,
    DateTime OccurredAt,
    DateTime CreatedAt,
    string IdempotencyKey);
