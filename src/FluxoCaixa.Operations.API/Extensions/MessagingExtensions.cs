using FluxoCaixa.Operations.Application.Contracts;
using MassTransit;

namespace FluxoCaixa.Operations.API.Extensions;

internal static class MessagingExtensions
{
    /// <summary>
    /// Registra MassTransit com transporte RabbitMQ para publicação de eventos do Outbox.
    /// Exchange canônico "transaction-created" garante roteamento correto para o consumer Consolidation.
    /// </summary>
    internal static IServiceCollection AddOperationsMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(
                    configuration["RabbitMQ:Host"] ?? "localhost",
                    "/",
                    h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });

                cfg.Message<TransactionCreatedMessage>(m =>
                    m.SetEntityName("transaction-created"));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
