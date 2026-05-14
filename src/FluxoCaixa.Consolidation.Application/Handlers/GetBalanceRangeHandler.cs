using FluxoCaixa.Consolidation.Application.Abstractions;
using FluxoCaixa.Consolidation.Application.Dtos;
using FluxoCaixa.Consolidation.Application.Queries;
using FluxoCaixa.Consolidation.Domain.Common;
using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Consolidation.Application.Handlers;

public sealed class GetBalanceRangeHandler
    : IQueryHandler<GetBalanceRangeQuery, IList<DailyBalanceDto>>
{
    private readonly IDailyBalanceRepository _repository;
    private readonly ILogger<GetBalanceRangeHandler> _logger;

    public GetBalanceRangeHandler(
        IDailyBalanceRepository repository,
        ILogger<GetBalanceRangeHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<IList<DailyBalanceDto>>> HandleAsync(
        GetBalanceRangeQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.MerchantId == Guid.Empty)
        {
            return Result.Failure<IList<DailyBalanceDto>>(
                Error.Validation("GetBalanceRange.MerchantId", "O ID do comerciante é obrigatório."));
        }

        if (query.From > query.To)
        {
            return Result.Failure<IList<DailyBalanceDto>>(
                Error.Validation(
                    "GetBalanceRange.InvalidRange",
                    "A data de início não pode ser posterior à data de fim."));
        }

        var from = DailyBalanceDate.From(query.From);
        var to = DailyBalanceDate.From(query.To);

        _logger.LogDebug(
            "Buscando saldos diários para comerciante {MerchantId} de {From} a {To}.",
            query.MerchantId, query.From, query.To);

        var balances = await _repository.FindByMerchantAndDateRangeAsync(
            query.MerchantId, from, to, cancellationToken);

        var dtos = balances
            .Select(b => new DailyBalanceDto(
                b.Id,
                b.MerchantId,
                b.Date.Value,
                b.TotalCredits,
                b.TotalDebits,
                b.Balance,
                b.LastUpdatedAt))
            .ToList();

        return Result.Success<IList<DailyBalanceDto>>(dtos);
    }
}
