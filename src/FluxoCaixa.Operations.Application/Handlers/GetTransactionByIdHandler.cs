using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Application.Dtos;
using FluxoCaixa.Operations.Application.Queries;
using FluxoCaixa.Operations.Domain.Common;
using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Repositories;
using FluxoCaixa.Operations.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Operations.Application.Handlers;

public sealed class GetTransactionByIdHandler : IQueryHandler<GetTransactionByIdQuery, TransactionDto>
{
    private readonly ITransactionRepository _repository;
    private readonly ILogger<GetTransactionByIdHandler> _logger;

    public GetTransactionByIdHandler(
        ITransactionRepository repository,
        ILogger<GetTransactionByIdHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> HandleAsync(
        GetTransactionByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _repository.FindByIdAsync(
            TransactionId.From(query.TransactionId), cancellationToken);

        if (transaction is null)
        {
            _logger.LogWarning("Transação {TransactionId} não encontrada.", query.TransactionId);
            return Result.Failure<TransactionDto>(
                Error.NotFound("Transaction.NotFound", $"Transação '{query.TransactionId}' não encontrada."));
        }

        return Result.Success(ToDto(transaction));
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
