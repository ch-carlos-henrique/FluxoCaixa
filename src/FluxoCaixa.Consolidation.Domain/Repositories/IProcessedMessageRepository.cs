namespace FluxoCaixa.Consolidation.Domain.Repositories;

public interface IProcessedMessageRepository
{
    Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task AddAsync(Guid messageId, CancellationToken cancellationToken = default);
}
