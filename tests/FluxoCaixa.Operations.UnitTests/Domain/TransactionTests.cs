using FluentAssertions;
using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.Events;
using FluxoCaixa.Operations.Domain.ValueObjects;

namespace FluxoCaixa.Operations.UnitTests.Domain;

public sealed class TransactionTests
{
    private static readonly Guid _merchantId = Guid.NewGuid();
    private static readonly Money _validMoney = Money.Create(100m, "BRL").Value;
    private static readonly IdempotencyKey _validKey = IdempotencyKey.Create("key-001").Value;
    private static readonly DateTime _validDate = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateCredit_WithValidData_ShouldSucceed()
    {
        var result = Transaction.CreateCredit(_merchantId, _validMoney, "desc", _validDate, _validKey);

        result.IsSuccess.Should().BeTrue();
        result.Value.MerchantId.Should().Be(_merchantId);
        result.Value.Type.Value.Should().Be("Credit");
        result.Value.Amount.Amount.Should().Be(100m);
        result.Value.Amount.Currency.Should().Be("BRL");
        result.Value.Description.Should().Be("desc");
        result.Value.OccurredAt.Should().Be(_validDate);
    }

    [Fact]
    public void CreateCredit_ShouldRaiseTransactionCreatedEvent()
    {
        var result = Transaction.CreateCredit(_merchantId, _validMoney, null, _validDate, _validKey);

        result.IsSuccess.Should().BeTrue();
        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TransactionCreatedEvent>();
    }

    [Fact]
    public void CreateCredit_WithEmptyMerchantId_ShouldFail()
    {
        var result = Transaction.CreateCredit(Guid.Empty, _validMoney, null, _validDate, _validKey);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Transaction.InvalidMerchantId");
    }

    [Fact]
    public void CreateDebit_WithValidData_ShouldSucceed()
    {
        var result = Transaction.CreateDebit(_merchantId, _validMoney, "desc", _validDate, _validKey);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Value.Should().Be("Debit");
    }

    [Fact]
    public void CreateDebit_WithEmptyMerchantId_ShouldFail()
    {
        var result = Transaction.CreateDebit(Guid.Empty, _validMoney, null, _validDate, _validKey);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Transaction.InvalidMerchantId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Money_Create_WithNonPositiveAmount_ShouldFail(decimal amount)
    {
        var result = Money.Create(amount, "BRL");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Money.InvalidAmount");
    }

    [Theory]
    [InlineData("BR")]
    [InlineData("BRLL")]
    [InlineData("")]
    public void Money_Create_WithInvalidCurrency_ShouldFail(string currency)
    {
        var result = Money.Create(100m, currency);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Money.InvalidCurrency");
    }

    [Fact]
    public void IdempotencyKey_Create_WithEmptyKey_ShouldFail()
    {
        var result = IdempotencyKey.Create(string.Empty);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IdempotencyKey_Create_WithKeyExceeding128Chars_ShouldFail()
    {
        var longKey = new string('a', 129);
        var result = IdempotencyKey.Create(longKey);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IdempotencyKey_Create_WithValidKey_ShouldSucceed()
    {
        var result = IdempotencyKey.Create("valid-key-123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("valid-key-123");
    }
}
