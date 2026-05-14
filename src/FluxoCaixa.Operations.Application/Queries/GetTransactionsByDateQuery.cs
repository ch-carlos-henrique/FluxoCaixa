namespace FluxoCaixa.Operations.Application.Queries;

public sealed record GetTransactionsByDateQuery(Guid MerchantId, DateTime From, DateTime To);
