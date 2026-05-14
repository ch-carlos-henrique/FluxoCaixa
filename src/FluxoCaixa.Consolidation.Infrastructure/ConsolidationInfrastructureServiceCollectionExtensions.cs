using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Infrastructure.Persistence;
using FluxoCaixa.Consolidation.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FluxoCaixa.Consolidation.Infrastructure;

public static class ConsolidationInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra os serviços de infraestrutura de Consolidação:
    /// DbContext e repositórios.
    /// O registro do TransactionCreatedConsumer no MassTransit é feito na camada de host (API).
    /// </summary>
    public static IServiceCollection AddConsolidationInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<DailyConsolidationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    "__ef_migrations_history_cons",
                    "public")));

        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();

        return services;
    }

    /// <summary>
    /// Aplica migrations pendentes no banco de dados de Consolidação.
    /// Deve ser chamado no startup da API, antes de receber requisições.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DailyConsolidationDbContext>();
        await db.Database.MigrateAsync();
    }
}
