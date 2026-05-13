using FluentValidation;
using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Application.Commands;
using FluxoCaixa.Operations.Application.Dtos;
using FluxoCaixa.Operations.Domain.Common;
using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Repositories;
using FluxoCaixa.Operations.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Operations.Application.Handlers;

public sealed class CreateTransactionHandler : ICommandHandler<CreateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _repository;
    private readonly IValidator<CreateTransactionCommand> _validator;
    private readonly ILogger<CreateTransactionHandler> _logger;

    public CreateTransactionHandler(
        ITransactionRepository repository,
        IValidator<CreateTransactionCommand> validator,
        ILogger<CreateTransactionHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> HandleAsync(
        CreateTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Validação via FluentValidation
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var firstError = validation.Errors[0];
            _logger.LogWarning(
                "Validação falhou para CreateTransactionCommand: {ErrorCode} — {ErrorMessage}",
                firstError.ErrorCode,
                firstError.ErrorMessage);
            return Result.Failure<TransactionDto>(
                Error.Validation(firstError.ErrorCode, firstError.ErrorMessage));
        }

        // 2. Construir value objects
        var moneyResult = Money.Create(command.Amount, command.Currency);
        if (moneyResult.IsFailure)
        {
            return Result.Failure<TransactionDto>(moneyResult.Error);
        }

        var keyResult = IdempotencyKey.Create(command.IdempotencyKey);
        if (keyResult.IsFailure)
        {
            return Result.Failure<TransactionDto>(keyResult.Error);
        }

        // 3. Verificação de idempotência
        var existing = await _repository.FindByIdempotencyKeyAsync(
            command.MerchantId, keyResult.Value, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Transação já existe para chave de idempotência {Key}. Retornando existente.",
                command.IdempotencyKey);
            return Result.Success(ToDto(existing));
        }

        // 4. Criar entidade de domínio
        var typeResult = TransactionType.FromString(command.Type);
        if (typeResult.IsFailure)
        {
            return Result.Failure<TransactionDto>(typeResult.Error);
        }

        var transactionResult = typeResult.Value.IsCredit
            ? Transaction.CreateCredit(
                command.MerchantId, moneyResult.Value, command.Description,
                command.OccurredAt, keyResult.Value)
            : Transaction.CreateDebit(
                command.MerchantId, moneyResult.Value, command.Description,
                command.OccurredAt, keyResult.Value);

        if (transactionResult.IsFailure)
        {
            return Result.Failure<TransactionDto>(transactionResult.Error);
        }

        // 5. Persistir
        await _repository.AddAsync(transactionResult.Value, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Transação {TransactionId} criada para comerciante {MerchantId}.",
            transactionResult.Value.Id.Value,
            command.MerchantId);

        return Result.Success(ToDto(transactionResult.Value));
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
