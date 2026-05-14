using FluentAssertions;
using FluxoCaixa.Consolidation.Application.Handlers;
using FluxoCaixa.Consolidation.Application.Queries;
using FluxoCaixa.Consolidation.Domain.Entities;
using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FluxoCaixa.Consolidation.UnitTests.Handlers;

public sealed class GetDailyBalanceHandlerTests
{
    private readonly IDailyBalanceRepository _repository = Substitute.For<IDailyBalanceRepository>();

    private GetDailyBalanceHandler CreateSut() =>
        new(_repository, NullLogger<GetDailyBalanceHandler>.Instance);

    private static readonly Guid _merchantId = Guid.NewGuid();
    private static readonly DateOnly _date = new(2026, 5, 1);

    private static DailyBalance BuildBalance()
    {
        var balance = DailyBalance.CreateForMerchant(_merchantId, DailyBalanceDate.From(_date));
        balance.Apply("Credit", 500m);
        balance.Apply("Debit", 200m);
        return balance;
    }

    [Fact]
    public async Task Handle_WhenBalanceExists_ShouldReturnDto()
    {
        var balance = BuildBalance();
        _repository.FindByMerchantAndDateAsync(_merchantId, Arg.Any<DailyBalanceDate>(), Arg.Any<CancellationToken>())
            .Returns(balance);

        var result = await CreateSut().HandleAsync(new GetDailyBalanceQuery(_merchantId, _date));

        result.IsSuccess.Should().BeTrue();
        result.Value.MerchantId.Should().Be(_merchantId);
        result.Value.TotalCredits.Should().Be(500m);
        result.Value.TotalDebits.Should().Be(200m);
        result.Value.Balance.Should().Be(300m);
    }

    [Fact]
    public async Task Handle_WhenBalanceNotFound_ShouldReturnNotFoundError()
    {
        _repository.FindByMerchantAndDateAsync(_merchantId, Arg.Any<DailyBalanceDate>(), Arg.Any<CancellationToken>())
            .Returns((DailyBalance?)null);

        var result = await CreateSut().HandleAsync(new GetDailyBalanceQuery(_merchantId, _date));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("GetDailyBalance.NotFound");
    }

    [Fact]
    public async Task Handle_WithEmptyMerchantId_ShouldReturnValidationError()
    {
        var result = await CreateSut().HandleAsync(new GetDailyBalanceQuery(Guid.Empty, _date));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("GetDailyBalance.MerchantId");
    }
}
