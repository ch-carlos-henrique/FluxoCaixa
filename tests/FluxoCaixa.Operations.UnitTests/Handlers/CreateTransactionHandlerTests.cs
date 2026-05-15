using Shouldly;
using FluentValidation;
using FluentValidation.Results;
using FluxoCaixa.Operations.Application.Commands;
using FluxoCaixa.Operations.Application.Handlers;
using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Repositories;
using FluxoCaixa.Operations.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FluxoCaixa.Operations.UnitTests.Handlers;

public sealed class CreateTransactionHandlerTests
{
    private readonly ITransactionRepository _repository = Substitute.For<ITransactionRepository>();
    private readonly IValidator<CreateTransactionCommand> _validator = Substitute.For<IValidator<CreateTransactionCommand>>();

    private CreateTransactionHandler CreateSut() =>
        new(_repository, _validator, NullLogger<CreateTransactionHandler>.Instance);

    private static CreateTransactionCommand ValidCommand(string idempotencyKey = "key-001") => new(
        MerchantId: Guid.NewGuid(),
        Type: "Credit",
        Amount: 100m,
        Currency: "BRL",
        Description: "Test",
        OccurredAt: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        IdempotencyKey: idempotencyKey);

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPersistAndReturnDto()
    {
        var command = ValidCommand();
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _repository.FindByIdempotencyKeyAsync(
            command.MerchantId, Arg.Any<IdempotencyKey>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var sut = CreateSut();
        var result = await sut.HandleAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.MerchantId.ShouldBe(command.MerchantId);
        result.Value.Type.ShouldBe("Credit");
        result.Value.Amount.ShouldBe(100m);
        await _repository.Received(1).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateIdempotencyKey_ShouldReturnExistingWithoutPersisting()
    {
        var command = ValidCommand("dup-key");
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var existingMoney = Money.Create(100m, "BRL").Value;
        var existingKey = IdempotencyKey.Create("dup-key").Value;
        var existing = Transaction.CreateCredit(
            command.MerchantId, existingMoney, null,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), existingKey).Value;

        _repository.FindByIdempotencyKeyAsync(
            command.MerchantId, Arg.Any<IdempotencyKey>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var sut = CreateSut();
        var result = await sut.HandleAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IdempotencyKey.ShouldBe("dup-key");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ShouldReturnValidationError()
    {
        var command = ValidCommand();
        var failure = new ValidationFailure("Amount", "O valor deve ser maior que zero.")
        {
            ErrorCode = "AmountGreaterThanZero",
        };
        _validator.ValidateAsync(command, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { failure }));

        var sut = CreateSut();
        var result = await sut.HandleAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("AmountGreaterThanZero");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }
}
