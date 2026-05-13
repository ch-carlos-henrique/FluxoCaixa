using FluentAssertions;
using FluxoCaixa.Operations.Application.Commands;

namespace FluxoCaixa.Operations.UnitTests.Validators;

public sealed class CreateTransactionCommandValidatorTests
{
    private readonly CreateTransactionCommandValidator _sut = new();

    private static CreateTransactionCommand ValidCommand() => new(
        MerchantId: Guid.NewGuid(),
        Type: "Credit",
        Amount: 100.00m,
        Currency: "BRL",
        Description: "Venda",
        OccurredAt: DateTime.UtcNow,
        IdempotencyKey: Guid.NewGuid().ToString());

    [Fact]
    public async Task Validator_CommandValido_PassaNaValidacao()
    {
        var result = await _sut.ValidateAsync(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task Validator_AmountMenorOuIgualZero_FalhaNaValidacao(decimal amount)
    {
        var command = ValidCommand() with { Amount = amount };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.Amount));
    }

    [Fact]
    public async Task Validator_MerchantIdVazio_FalhaNaValidacao()
    {
        var command = ValidCommand() with { MerchantId = Guid.Empty };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.MerchantId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Transferencia")]
    [InlineData("credit")]   // case-sensitive
    [InlineData("DEBIT")]    // case-sensitive
    public async Task Validator_TipoInvalido_FalhaNaValidacao(string type)
    {
        var command = ValidCommand() with { Type = type };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Type));
    }

    [Theory]
    [InlineData("Credit")]
    [InlineData("Debit")]
    public async Task Validator_TiposValidos_PassaNaValidacao(string type)
    {
        var command = ValidCommand() with { Type = type };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("BR")]       // menos de 3 letras
    [InlineData("BRLX")]     // mais de 3 letras
    public async Task Validator_MoedaInvalida_FalhaNaValidacao(string currency)
    {
        var command = ValidCommand() with { Currency = currency };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Currency));
    }

    [Fact]
    public async Task Validator_OccurredAtDefault_FalhaNaValidacao()
    {
        var command = ValidCommand() with { OccurredAt = default };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.OccurredAt));
    }

    [Fact]
    public async Task Validator_IdempotencyKeyVazia_FalhaNaValidacao()
    {
        var command = ValidCommand() with { IdempotencyKey = string.Empty };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.IdempotencyKey));
    }

    [Fact]
    public async Task Validator_IdempotencyKeyMaisDe128Caracteres_FalhaNaValidacao()
    {
        var command = ValidCommand() with { IdempotencyKey = new string('x', 129) };

        var result = await _sut.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.IdempotencyKey));
    }
}
