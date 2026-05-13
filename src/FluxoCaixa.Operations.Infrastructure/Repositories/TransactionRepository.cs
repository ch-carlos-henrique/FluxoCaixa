using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Repositories;
using FluxoCaixa.Operations.Domain.ValueObjects;
using FluxoCaixa.Operations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Operations.Infrastructure.Repositories;

internal sealed class TransactionRepository : ITransactionRepository
{
    private readonly TransactionDbContext _context;

    public TransactionRepository(TransactionDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> FindByIdAsync(
        TransactionId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashEntries
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Transaction?> FindByIdempotencyKeyAsync(
        Guid merchantId,
        IdempotencyKey key,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashEntries
            .FirstOrDefaultAsync(
                t => t.MerchantId == merchantId && t.IdempotencyKey == key,
                cancellationToken);
    }

    public async Task<IList<Transaction>> FindByMerchantAndDateAsync(
        Guid merchantId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        return await _context.CashEntries
            .Where(t => t.MerchantId == merchantId
                        && t.OccurredAt >= from
                        && t.OccurredAt <= to)
            .OrderBy(t => t.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        await _context.CashEntries.AddAsync(transaction, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
