using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FluxoCaixa.Operations.Infrastructure.Persistence;

/// <summary>
/// Fábrica usada pelo dotnet-ef em tempo de design para criar o TransactionDbContext
/// sem necessitar de um host em execução. Conexão de desenvolvimento local.
/// </summary>
internal sealed class TransactionDbContextFactory : IDesignTimeDbContextFactory<TransactionDbContext>
{
    public TransactionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TransactionDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=fluxocaixa_ops;Username=postgres;Password=postgres",
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                "__ef_migrations_history_ops",
                "public"));

        return new TransactionDbContext(optionsBuilder.Options);
    }
}
