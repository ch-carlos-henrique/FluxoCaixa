namespace FluxoCaixa.Consolidation.Application.Queries;

public sealed record GetDailyBalanceQuery(Guid MerchantId, DateOnly Date);
