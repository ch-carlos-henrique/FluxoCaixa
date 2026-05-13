using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Infrastructure.Persistence;
using FluxoCaixa.Consolidation.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
}
