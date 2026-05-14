using FluxoCaixa.Operations.Application.Contracts;
using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Events;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FluxoCaixa.Operations.Infrastructure.Persistence;

public sealed class TransactionDbContext : DbContext
{
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> CashEntries => Set<Transaction>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransactionDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Converte domain events de Transaction em outbox messages na mesma transação.
        var transactions = ChangeTracker
            .Entries<Transaction>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var transaction in transactions)
        {
            foreach (var domainEvent in transaction.DomainEvents)
            {
                if (domainEvent is TransactionCreatedEvent evt)
                {
                    var message = new TransactionCreatedMessage(
                        Guid.NewGuid(),
                        evt.TransactionId.Value,
                        evt.MerchantId,
                        evt.Type.Value,
                        evt.Amount.Amount,
                        evt.Amount.Currency,
                        evt.OccurredAt);

                    OutboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = "TransactionCreated",
                        Payload = JsonSerializer.Serialize(message),
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow,
                        RetryCount = 0,
                    });
                }
            }

            transaction.ClearDomainEvents();
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
