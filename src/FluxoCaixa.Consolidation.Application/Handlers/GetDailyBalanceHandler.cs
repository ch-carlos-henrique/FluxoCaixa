using FluxoCaixa.Consolidation.Application.Abstractions;
using FluxoCaixa.Consolidation.Application.Dtos;
using FluxoCaixa.Consolidation.Application.Queries;
using FluxoCaixa.Consolidation.Domain.Common;
using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Consolidation.Application.Handlers;

public sealed class GetDailyBalanceHandler
    : IQueryHandler<GetDailyBalanceQuery, DailyBalanceDto>
{
    private readonly IDailyBalanceRepository _repository;
    private readonly ILogger<GetDailyBalanceHandler> _logger;

    public GetDailyBalanceHandler(
        IDailyBalanceRepository repository,
        ILogger<GetDailyBalanceHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<DailyBalanceDto>> HandleAsync(
        GetDailyBalanceQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.MerchantId == Guid.Empty)
        {
            return Result.Failure<DailyBalanceDto>(
                Error.Validation("GetDailyBalance.MerchantId", "O ID do comerciante é obrigatório."));
        }

        var date = DailyBalanceDate.From(query.Date);

        _logger.LogDebug(
            "Buscando saldo diário para comerciante {MerchantId} na data {Date}.",
            query.MerchantId, query.Date);

        var balance = await _repository.FindByMerchantAndDateAsync(
            query.MerchantId, date, cancellationToken);

        if (balance is null)
        {
            return Result.Failure<DailyBalanceDto>(
                Error.NotFound(
                    "GetDailyBalance.NotFound",
                    $"Saldo diário não encontrado para o comerciante '{query.MerchantId}' na data '{query.Date}'."));
        }

        var dto = new DailyBalanceDto(
            balance.Id,
            balance.MerchantId,
            balance.Date.Value,
            balance.TotalCredits,
            balance.TotalDebits,
            balance.Balance,
            balance.LastUpdatedAt);

        return Result.Success(dto);
    }
}
