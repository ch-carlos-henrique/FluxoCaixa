namespace FluxoCaixa.Consolidation.Domain.ValueObjects;

public sealed record DailyBalanceDate
{
    public DateOnly Value { get; }

    private DailyBalanceDate(DateOnly value) => Value = value;

    public static DailyBalanceDate From(DateOnly value) => new(value);
    public static DailyBalanceDate Today() => new(DateOnly.FromDateTime(DateTime.UtcNow));

    public override string ToString() => Value.ToString("yyyy-MM-dd");
}
