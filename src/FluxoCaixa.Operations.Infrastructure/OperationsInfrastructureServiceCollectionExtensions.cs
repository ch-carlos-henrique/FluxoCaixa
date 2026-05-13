using FluxoCaixa.Operations.Domain.Repositories;
using FluxoCaixa.Operations.Infrastructure.Messaging;
using FluxoCaixa.Operations.Infrastructure.Persistence;
using FluxoCaixa.Operations.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoCaixa.Operations.Infrastructure;

public static class OperationsInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra os serviços de infraestrutura de Operações:
    /// DbContext, repositórios e o OutboxPublisherWorker.
    /// A configuração do transporte MassTransit (RabbitMQ) é feita na camada de host (API).
    /// </summary>
    public static IServiceCollection AddOperationsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TransactionDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                    "__ef_migrations_history_ops",
                    "public")));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }
}
