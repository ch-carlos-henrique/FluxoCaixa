using FluxoCaixa.Operations.Domain.Common;

namespace FluxoCaixa.Operations.Domain.ValueObjects;

public sealed record IdempotencyKey
{
    public string Value { get; }

    private IdempotencyKey(string value) => Value = value;

    public static Result<IdempotencyKey> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<IdempotencyKey>(
                Error.Validation("IdempotencyKey.Empty", "A chave de idempotência não pode ser vazia."));
        }

        if (value.Length > 128)
        {
            return Result.Failure<IdempotencyKey>(
                Error.Validation("IdempotencyKey.TooLong", "A chave de idempotência não pode exceder 128 caracteres."));
        }

        return Result.Success(new IdempotencyKey(value));
    }

    public override string ToString() => Value;
}
