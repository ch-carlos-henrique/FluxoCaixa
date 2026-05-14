using FluentAssertions;
using FluxoCaixa.Consolidation.Domain.Entities;
using FluxoCaixa.Consolidation.Domain.ValueObjects;

namespace FluxoCaixa.Consolidation.UnitTests.Domain;

public sealed class DailyBalanceTests
{
    private static readonly Guid _merchantId = Guid.NewGuid();
    private static readonly DailyBalanceDate _date = DailyBalanceDate.From(new DateOnly(2026, 5, 1));

    private static DailyBalance CreateBalance() =>
        DailyBalance.CreateForMerchant(_merchantId, _date);

    [Fact]
    public void CreateForMerchant_WithValidData_ShouldReturnBalanceWithZeroTotals()
    {
        var balance = CreateBalance();

        balance.MerchantId.Should().Be(_merchantId);
        balance.Date.Value.Should().Be(_date.Value);
        balance.TotalCredits.Should().Be(0m);
        balance.TotalDebits.Should().Be(0m);
        balance.Balance.Should().Be(0m);
    }

    [Fact]
    public void CreateForMerchant_WithEmptyMerchantId_ShouldThrowArgumentException()
    {
        var act = () => DailyBalance.CreateForMerchant(Guid.Empty, _date);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apply_CreditTransaction_ShouldIncreaseTotalCredits()
    {
        var balance = CreateBalance();

        balance.Apply("Credit", 150m);

        balance.TotalCredits.Should().Be(150m);
        balance.TotalDebits.Should().Be(0m);
        balance.Balance.Should().Be(150m);
    }

    [Fact]
    public void Apply_DebitTransaction_ShouldIncreaseTotalDebits()
    {
        var balance = CreateBalance();

        balance.Apply("Debit", 80m);

        balance.TotalCredits.Should().Be(0m);
        balance.TotalDebits.Should().Be(80m);
        balance.Balance.Should().Be(-80m);
    }

    [Fact]
    public void Apply_MultipleTransactions_ShouldAccumulateCorrectly()
    {
        var balance = CreateBalance();

        balance.Apply("Credit", 200m);
        balance.Apply("Credit", 50m);
        balance.Apply("Debit", 70m);

        balance.TotalCredits.Should().Be(250m);
        balance.TotalDebits.Should().Be(70m);
        balance.Balance.Should().Be(180m);
    }

    [Fact]
    public void Balance_ShouldAlwaysEqualCreditMinusDebits()
    {
        var balance = CreateBalance();
        balance.Apply("Credit", 300m);
        balance.Apply("Debit", 120m);

        balance.Balance.Should().Be(balance.TotalCredits - balance.TotalDebits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_WithNonPositiveAmount_ShouldThrowInvalidOperationException(decimal amount)
    {
        var balance = CreateBalance();

        var act = () => balance.Apply("Credit", amount);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Apply_WithUnknownTransactionType_ShouldThrowInvalidOperationException()
    {
        var balance = CreateBalance();

        var act = () => balance.Apply("Unknown", 100m);

        act.Should().Throw<InvalidOperationException>();
    }
}
