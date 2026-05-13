using BCrypt.Net;
using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Domain.Repositories;
using FluxoCaixa.Operations.Infrastructure.Messaging;
using FluxoCaixa.Operations.Infrastructure.Persistence;
using FluxoCaixa.Operations.Infrastructure.Repositories;
using FluxoCaixa.Operations.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FluxoCaixa.Operations.Infrastructure;

public static class OperationsInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra os serviços de infraestrutura de Operações:
    /// DbContext, repositórios, AuthService e o OutboxPublisherWorker.
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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }

    /// <summary>
    /// Aplica migrations pendentes e semeia dados de desenvolvimento (usuários de teste).
    /// Deve ser chamado no startup da API, antes de receber requisições.
    /// </summary>
    public static async Task ApplyMigrationsAndSeedAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Id           = Guid.NewGuid(),
                Email        = "admin@fluxocaixa.dev",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123", workFactor: 12),
                Role         = "Admin",
                MerchantId   = Guid.Empty,
            });

            db.Users.Add(new User
            {
                Id           = Guid.NewGuid(),
                Email        = "merchant@fluxocaixa.dev",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Merchant@123", workFactor: 12),
                Role         = "Merchant",
                MerchantId   = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            });

            // 20 usuários merchant para o teste de carga k6 (1 token por VU = sem conflito de rate limit)
            for (var i = 1; i <= 20; i++)
            {
                db.Users.Add(new User
                {
                    Id           = Guid.NewGuid(),
                    Email        = $"merchant{i:D2}@fluxocaixa.dev",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Merchant@123", workFactor: 12),
                    Role         = "Merchant",
                    MerchantId   = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                });
            }

            await db.SaveChangesAsync();
        }
    }
}
