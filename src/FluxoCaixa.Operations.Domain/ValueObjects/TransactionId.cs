namespace FluxoCaixa.Operations.Domain.ValueObjects;

public sealed record TransactionId(Guid Value)
{
    public static TransactionId New() => new(Guid.NewGuid());
    public static TransactionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
