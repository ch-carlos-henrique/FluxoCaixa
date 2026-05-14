using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Application.Dtos;
using FluxoCaixa.Operations.Application.Queries;
using FluxoCaixa.Operations.Domain.Common;
using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Operations.Application.Handlers;

public sealed class GetTransactionsByDateHandler
    : IQueryHandler<GetTransactionsByDateQuery, IList<TransactionDto>>
{
    private readonly ITransactionRepository _repository;
    private readonly ILogger<GetTransactionsByDateHandler> _logger;

    public GetTransactionsByDateHandler(
        ITransactionRepository repository,
        ILogger<GetTransactionsByDateHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<IList<TransactionDto>>> HandleAsync(
        GetTransactionsByDateQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.From > query.To)
        {
            return Result.Failure<IList<TransactionDto>>(
                Error.Validation(
                    "GetTransactions.InvalidDateRange",
                    "A data de início não pode ser posterior à data de fim."));
        }

        var transactions = await _repository.FindByMerchantAndDateAsync(
            query.MerchantId, query.From, query.To, cancellationToken);

        _logger.LogInformation(
            "Encontradas {Count} transações para o comerciante {MerchantId} entre {From} e {To}.",
            transactions.Count,
            query.MerchantId,
            query.From,
            query.To);

        IList<TransactionDto> dtos = transactions.Select(ToDto).ToList();
        return Result.Success(dtos);
    }

    private static TransactionDto ToDto(Transaction transaction) => new(
        transaction.Id.Value,
        transaction.MerchantId,
        transaction.Type.Value,
        transaction.Amount.Amount,
        transaction.Amount.Currency,
        transaction.Description,
        transaction.OccurredAt,
        transaction.CreatedAt,
        transaction.IdempotencyKey.Value);
}
