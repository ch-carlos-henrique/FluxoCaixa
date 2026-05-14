using FluxoCaixa.Operations.Domain.ValueObjects;

namespace FluxoCaixa.Operations.Domain.Events;

public sealed class TransactionCreatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public TransactionId TransactionId { get; }
    public Guid MerchantId { get; }
    public TransactionType Type { get; }
    public Money Amount { get; }
    public DateTime OccurredAt { get; }

    public TransactionCreatedEvent(
        TransactionId transactionId,
        Guid merchantId,
        TransactionType type,
        Money amount,
        DateTime occurredAt)
    {
        TransactionId = transactionId;
        MerchantId = merchantId;
        Type = type;
        Amount = amount;
        OccurredAt = occurredAt;
    }
}
