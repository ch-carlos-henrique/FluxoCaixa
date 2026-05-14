using FluxoCaixa.Operations.Domain.Common;

namespace FluxoCaixa.Operations.Domain.ValueObjects;

public sealed record TransactionType
{
    public static readonly TransactionType Credit = new("Credit");
    public static readonly TransactionType Debit = new("Debit");

    private static readonly Dictionary<string, TransactionType> _all =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Credit.Value] = Credit,
            [Debit.Value] = Debit,
        };

    private TransactionType(string value) => Value = value;

    public string Value { get; }
    public bool IsCredit => this == Credit;
    public bool IsDebit => this == Debit;

    public static Result<TransactionType> FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<TransactionType>(
                Error.Validation("TransactionType.Empty", "O tipo de transação não pode ser vazio."));
        }

        if (_all.TryGetValue(value, out var type))
        {
            return Result.Success(type);
        }

        return Result.Failure<TransactionType>(
            Error.Validation(
                "TransactionType.Invalid",
                $"'{value}' não é um tipo de transação válido. Valores aceitos: Credit, Debit."));
    }

    public override string ToString() => Value;
}
