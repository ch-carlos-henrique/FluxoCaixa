using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FluxoCaixa.Consolidation.Infrastructure.Persistence;

/// <summary>
/// Fábrica usada pelo dotnet-ef em tempo de design para criar o DailyConsolidationDbContext
/// sem necessitar de um host em execução. Conexão de desenvolvimento local.
/// </summary>
internal sealed class DailyConsolidationDbContextFactory
    : IDesignTimeDbContextFactory<DailyConsolidationDbContext>
{
    public DailyConsolidationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DailyConsolidationDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=fluxocaixa_cons;Username=postgres;Password=postgres",
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                "__ef_migrations_history_cons",
                "public"));

        return new DailyConsolidationDbContext(optionsBuilder.Options);
    }
}
