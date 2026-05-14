using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Consolidation.Infrastructure.Repositories;

internal sealed class ProcessedMessageRepository : IProcessedMessageRepository
{
    private readonly DailyConsolidationDbContext _context;

    public ProcessedMessageRepository(DailyConsolidationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProcessedMessages
            .AnyAsync(m => m.EventId == messageId, cancellationToken);
    }

    public async Task AddAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await _context.ProcessedMessages.AddAsync(
            new ProcessedMessage
            {
                Id = Guid.NewGuid(),
                EventId = messageId,
                ProcessedAt = DateTime.UtcNow,
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
