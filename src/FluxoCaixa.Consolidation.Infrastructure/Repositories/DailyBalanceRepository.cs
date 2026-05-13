using FluxoCaixa.Consolidation.Domain.Entities;
using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Domain.ValueObjects;
using FluxoCaixa.Consolidation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Consolidation.Infrastructure.Repositories;

internal sealed class DailyBalanceRepository : IDailyBalanceRepository
{
    private readonly DailyConsolidationDbContext _context;

    public DailyBalanceRepository(DailyConsolidationDbContext context)
    {
        _context = context;
    }

    public async Task<DailyBalance?> FindByMerchantAndDateAsync(
        Guid merchantId,
        DailyBalanceDate date,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyBalances
            .FirstOrDefaultAsync(
                b => b.MerchantId == merchantId && b.Date == date,
                cancellationToken);
    }

    public async Task<IList<DailyBalance>> FindByMerchantAndDateRangeAsync(
        Guid merchantId,
        DailyBalanceDate from,
        DailyBalanceDate to,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyBalances
            .Where(b => b.MerchantId == merchantId
                        && b.Date.Value >= from.Value
                        && b.Date.Value <= to.Value)
            .OrderBy(b => b.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        DailyBalance dailyBalance,
        CancellationToken cancellationToken = default)
    {
        await _context.DailyBalances.AddAsync(dailyBalance, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
