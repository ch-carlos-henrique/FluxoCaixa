using FluxoCaixa.Consolidation.Application.Consumers;
using FluxoCaixa.Consolidation.Application.Contracts;
using MassTransit;

namespace FluxoCaixa.Consolidation.API.Extensions;

internal static class MessagingExtensions
{
    /// <summary>
    /// Registra MassTransit com transporte RabbitMQ e o consumer TransactionCreatedConsumer.
    /// Exchange canônico "transaction-created" deve ser idêntico ao declarado na Operations API.
    /// </summary>
    internal static IServiceCollection AddConsolidationMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<TransactionCreatedConsumer>();

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
