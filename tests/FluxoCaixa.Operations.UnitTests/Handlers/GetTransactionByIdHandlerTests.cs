using FluentAssertions;
using FluxoCaixa.Operations.Application.Handlers;
using FluxoCaixa.Operations.Application.Queries;
using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Repositories;
using FluxoCaixa.Operations.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FluxoCaixa.Operations.UnitTests.Handlers;

public sealed class GetTransactionByIdHandlerTests
{
    private readonly ITransactionRepository _repository = Substitute.For<ITransactionRepository>();

    private GetTransactionByIdHandler CreateSut() =>
        new(_repository, NullLogger<GetTransactionByIdHandler>.Instance);

    private static Transaction BuildTransaction(Guid merchantId)
    {
        var money = Money.Create(250m, "BRL").Value;
        var key = IdempotencyKey.Create("key-xyz").Value;
        return Transaction.CreateCredit(
            merchantId, money, "test",
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), key).Value;
    }

    [Fact]
    public async Task Handle_WithExistingId_ShouldReturnTransactionDto()
    {
        var merchantId = Guid.NewGuid();
        var transaction = BuildTransaction(merchantId);
        _repository.FindByIdAsync(transaction.Id, Arg.Any<CancellationToken>())
            .Returns(transaction);

        var result = await CreateSut().HandleAsync(new GetTransactionByIdQuery(transaction.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(transaction.Id.Value);
        result.Value.MerchantId.Should().Be(merchantId);
        result.Value.Type.Should().Be("Credit");
    }

    [Fact]
    public async Task Handle_WithNonExistingId_ShouldReturnNotFound()
    {
        _repository.FindByIdAsync(Arg.Any<TransactionId>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var result = await CreateSut().HandleAsync(new GetTransactionByIdQuery(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Transaction.NotFound");
    }
}
