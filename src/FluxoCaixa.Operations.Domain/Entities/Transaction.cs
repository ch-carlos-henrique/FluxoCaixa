using FluxoCaixa.Operations.Domain.Common;
using FluxoCaixa.Operations.Domain.Events;
using FluxoCaixa.Operations.Domain.ValueObjects;

namespace FluxoCaixa.Operations.Domain.Entities;

public sealed class Transaction
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public TransactionId Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public TransactionType Type { get; private set; }
    public Money Amount { get; private set; }
    public string? Description { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IdempotencyKey IdempotencyKey { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Transaction()
    {
        // Required by EF Core — values are populated from the database.
        Id = null!;
        Type = null!;
        Amount = null!;
        IdempotencyKey = null!;
    }

    private Transaction(
        TransactionId id,
        Guid merchantId,
        TransactionType type,
        Money amount,
        string? description,
        DateTime occurredAt,
        IdempotencyKey idempotencyKey)
    {
        Id = id;
        MerchantId = merchantId;
        Type = type;
        Amount = amount;
        Description = description;
        OccurredAt = occurredAt;
        CreatedAt = DateTime.UtcNow;
        IdempotencyKey = idempotencyKey;

        _domainEvents.Add(new TransactionCreatedEvent(id, merchantId, type, amount, occurredAt));
    }

    public static Result<Transaction> CreateCredit(
        Guid merchantId,
        Money amount,
        string? description,
        DateTime occurredAt,
        IdempotencyKey idempotencyKey)
    {
        if (merchantId == Guid.Empty)
        {
            return Result.Failure<Transaction>(
                Error.Validation("Transaction.InvalidMerchantId", "O ID do comerciante não pode ser vazio."));
        }

        return Result.Success(new Transaction(
            TransactionId.New(),
            merchantId,
            TransactionType.Credit,
            amount,
            description,
            occurredAt,
            idempotencyKey));
    }

    public static Result<Transaction> CreateDebit(
        Guid merchantId,
        Money amount,
        string? description,
        DateTime occurredAt,
        IdempotencyKey idempotencyKey)
    {
        if (merchantId == Guid.Empty)
        {
            return Result.Failure<Transaction>(
                Error.Validation("Transaction.InvalidMerchantId", "O ID do comerciante não pode ser vazio."));
        }

        return Result.Success(new Transaction(
            TransactionId.New(),
            merchantId,
            TransactionType.Debit,
            amount,
            description,
            occurredAt,
            idempotencyKey));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
