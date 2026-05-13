using FluentValidation;
using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Application.Commands;
using FluxoCaixa.Operations.Application.Dtos;
using FluxoCaixa.Operations.Application.Handlers;
using FluxoCaixa.Operations.Application.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoCaixa.Operations.Application;

public static class OperationsApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddOperationsApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateTransactionCommand, TransactionDto>, CreateTransactionHandler>();
        services.AddScoped<IQueryHandler<GetTransactionByIdQuery, TransactionDto>, GetTransactionByIdHandler>();
        services.AddScoped<IQueryHandler<GetTransactionsByDateQuery, IList<TransactionDto>>, GetTransactionsByDateHandler>();
        services.AddScoped<IValidator<CreateTransactionCommand>, CreateTransactionCommandValidator>();

        return services;
    }
}
