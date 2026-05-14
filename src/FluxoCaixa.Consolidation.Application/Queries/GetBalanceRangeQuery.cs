namespace FluxoCaixa.Consolidation.Application.Queries;

public sealed record GetBalanceRangeQuery(Guid MerchantId, DateOnly From, DateOnly To);
