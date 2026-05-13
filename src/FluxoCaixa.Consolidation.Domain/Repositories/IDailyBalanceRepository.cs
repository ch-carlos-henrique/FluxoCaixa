using FluxoCaixa.Consolidation.Domain.Entities;
using FluxoCaixa.Consolidation.Domain.ValueObjects;

namespace FluxoCaixa.Consolidation.Domain.Repositories;

public interface IDailyBalanceRepository
{
    Task<DailyBalance?> FindByMerchantAndDateAsync(
        Guid merchantId,
        DailyBalanceDate date,
        CancellationToken cancellationToken = default);

    Task<IList<DailyBalance>> FindByMerchantAndDateRangeAsync(
        Guid merchantId,
        DailyBalanceDate from,
        DailyBalanceDate to,
        CancellationToken cancellationToken = default);

    Task AddAsync(DailyBalance dailyBalance, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
