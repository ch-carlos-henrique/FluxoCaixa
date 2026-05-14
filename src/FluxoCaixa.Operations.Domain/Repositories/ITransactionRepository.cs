using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.ValueObjects;

namespace FluxoCaixa.Operations.Domain.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> FindByIdAsync(TransactionId id, CancellationToken cancellationToken = default);

    Task<Transaction?> FindByIdempotencyKeyAsync(
        Guid merchantId,
        IdempotencyKey key,
        CancellationToken cancellationToken = default);

    Task<IList<Transaction>> FindByMerchantAndDateAsync(
        Guid merchantId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
