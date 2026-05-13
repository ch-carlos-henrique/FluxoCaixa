using FluxoCaixa.Consolidation.Domain.ValueObjects;

namespace FluxoCaixa.Consolidation.Domain.Entities;

public sealed class DailyBalance
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public DailyBalanceDate Date { get; private set; }
    public decimal TotalCredits { get; private set; }
    public decimal TotalDebits { get; private set; }

    // Invariant: balance is always derived — never stored directly.
    public decimal Balance => TotalCredits - TotalDebits;

    public DateTime LastUpdatedAt { get; private set; }

    private DailyBalance()
    {
        // Required by EF Core — values are populated from the database.
        Date = null!;
    }

    public static DailyBalance CreateForMerchant(Guid merchantId, DailyBalanceDate date)
    {
        ArgumentNullException.ThrowIfNull(date);

        if (merchantId == Guid.Empty)
        {
            throw new ArgumentException("O ID do comerciante não pode ser vazio.", nameof(merchantId));
        }

        return new DailyBalance
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Date = date,
            TotalCredits = 0m,
            TotalDebits = 0m,
            LastUpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Applies a transaction event to this daily balance.
    /// Called by the Consolidation consumer when processing a TransactionCreated message.
    /// </summary>
    /// <param name="transactionType">"Credit" or "Debit"</param>
    /// <param name="amount">Positive transaction amount.</param>
    public void Apply(string transactionType, decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("O valor da transação deve ser maior que zero.");
        }

        if (transactionType == "Credit")
        {
            TotalCredits += amount;
        }
        else if (transactionType == "Debit")
        {
            TotalDebits += amount;
        }
        else
        {
            throw new InvalidOperationException(
                $"Tipo de transação desconhecido: '{transactionType}'. Esperado 'Credit' ou 'Debit'.");
        }

        LastUpdatedAt = DateTime.UtcNow;
    }
}
