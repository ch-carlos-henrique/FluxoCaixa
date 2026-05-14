using FluxoCaixa.Operations.Domain.Entities;
using FluxoCaixa.Operations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FluxoCaixa.Operations.Infrastructure.Persistence.Configurations;

internal sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("cash_entries");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => TransactionId.From(value));

        builder.Property(t => t.MerchantId)
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(t => t.Type)
            .HasColumnName("type")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion(
                new ValueConverter<TransactionType, string>(
                    type => type.Value,
                    value => value == "Credit" ? TransactionType.Credit : TransactionType.Debit));

        builder.OwnsOne(t => t.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(t => t.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired()
            .HasConversion(
                new ValueConverter<IdempotencyKey, string>(
                    key => key.Value,
                    value => IdempotencyKey.FromTrusted(value)));

        // Domain events não são mapeados — somente existem em memória.
        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(t => new { t.MerchantId, t.OccurredAt })
            .HasDatabaseName("ix_cash_entries_merchant_occurred");

        builder.HasIndex(t => new { t.MerchantId, t.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_cash_entries_merchant_idempotency")
            .HasFilter(null);
    }
}
