using FluxoCaixa.Operations.Domain.Common;

namespace FluxoCaixa.Operations.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string? currency)
    {
        if (amount <= 0)
        {
            return Result.Failure<Money>(
                Error.Validation("Money.InvalidAmount", "O valor deve ser maior que zero."));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<Money>(
                Error.Validation("Money.InvalidCurrency", "A moeda não pode ser vazia."));
        }

        if (currency.Length != 3)
        {
            return Result.Failure<Money>(
                Error.Validation("Money.InvalidCurrency", "A moeda deve ser um código ISO de 3 letras (ex: BRL)."));
        }

        return Result.Success(new Money(amount, currency.ToUpperInvariant()));
    }

    /// <summary>
    /// Creates a Money instance from already-validated data (e.g. loaded from DB).
    /// </summary>
    public static Money FromTrusted(decimal amount, string currency) => new(amount, currency);

    public override string ToString() => $"{Amount:F2} {Currency}";
}
