using FluxoCaixa.Consolidation.Application.Abstractions;
using FluxoCaixa.Consolidation.Application.Dtos;
using FluxoCaixa.Consolidation.Application.Handlers;
using FluxoCaixa.Consolidation.Application.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoCaixa.Consolidation.Application;

public static class ConsolidationApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidationApplication(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<GetDailyBalanceQuery, DailyBalanceDto>, GetDailyBalanceHandler>();
        services.AddScoped<IQueryHandler<GetBalanceRangeQuery, IList<DailyBalanceDto>>, GetBalanceRangeHandler>();

        return services;
    }
}
