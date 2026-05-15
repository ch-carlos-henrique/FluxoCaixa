using Shouldly;
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

        result.IsSuccess.ShouldBeTrue();
        result.Value.MerchantId.ShouldBe(_merchantId);
        result.Value.Type.Value.ShouldBe("Credit");
        result.Value.Amount.Amount.ShouldBe(100m);
        result.Value.Amount.Currency.ShouldBe("BRL");
        result.Value.Description.ShouldBe("desc");
        result.Value.OccurredAt.ShouldBe(_validDate);
    }

    [Fact]
    public void CreateCredit_ShouldRaiseTransactionCreatedEvent()
    {
        var result = Transaction.CreateCredit(_merchantId, _validMoney, null, _validDate, _validKey);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TransactionCreatedEvent>();
    }

    [Fact]
    public void CreateCredit_WithEmptyMerchantId_ShouldFail()
    {
        var result = Transaction.CreateCredit(Guid.Empty, _validMoney, null, _validDate, _validKey);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transaction.InvalidMerchantId");
    }

    [Fact]
    public void CreateDebit_WithValidData_ShouldSucceed()
    {
        var result = Transaction.CreateDebit(_merchantId, _validMoney, "desc", _validDate, _validKey);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.Value.ShouldBe("Debit");
    }

    [Fact]
    public void CreateDebit_WithEmptyMerchantId_ShouldFail()
    {
        var result = Transaction.CreateDebit(Guid.Empty, _validMoney, null, _validDate, _validKey);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transaction.InvalidMerchantId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Money_Create_WithNonPositiveAmount_ShouldFail(decimal amount)
    {
        var result = Money.Create(amount, "BRL");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.InvalidAmount");
    }

    [Theory]
    [InlineData("BR")]
    [InlineData("BRLL")]
    [InlineData("")]
    public void Money_Create_WithInvalidCurrency_ShouldFail(string currency)
    {
        var result = Money.Create(100m, currency);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.InvalidCurrency");
    }

    [Fact]
    public void IdempotencyKey_Create_WithEmptyKey_ShouldFail()
    {
        var result = IdempotencyKey.Create(string.Empty);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void IdempotencyKey_Create_WithKeyExceeding128Chars_ShouldFail()
    {
        var longKey = new string('a', 129);
        var result = IdempotencyKey.Create(longKey);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void IdempotencyKey_Create_WithValidKey_ShouldSucceed()
    {
        var result = IdempotencyKey.Create("valid-key-123");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("valid-key-123");
    }
}
