using FluxoCaixa.Consolidation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Consolidation.Infrastructure.Persistence;

public sealed class DailyConsolidationDbContext : DbContext
{
    public DailyConsolidationDbContext(DbContextOptions<DailyConsolidationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();

    internal DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DailyConsolidationDbContext).Assembly);
    }
}
