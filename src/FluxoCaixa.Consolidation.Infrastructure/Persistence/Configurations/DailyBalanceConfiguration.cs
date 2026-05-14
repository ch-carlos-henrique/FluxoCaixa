using FluxoCaixa.Consolidation.Domain.Entities;
using FluxoCaixa.Consolidation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FluxoCaixa.Consolidation.Infrastructure.Persistence.Configurations;

internal sealed class DailyBalanceConfiguration : IEntityTypeConfiguration<DailyBalance>
{
    public void Configure(EntityTypeBuilder<DailyBalance> builder)
    {
        builder.ToTable("daily_balances");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id");

        builder.Property(b => b.MerchantId)
            .HasColumnName("merchant_id")
            .IsRequired();

        builder.Property(b => b.Date)
            .HasColumnName("date")
            .HasColumnType("date")
            .IsRequired()
            .HasConversion(
                new ValueConverter<DailyBalanceDate, DateOnly>(
                    v => v.Value,
                    v => DailyBalanceDate.From(v)));

        builder.Property(b => b.TotalCredits)
            .HasColumnName("total_credits")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(b => b.TotalDebits)
            .HasColumnName("total_debits")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        // Balance é uma propriedade calculada (TotalCredits - TotalDebits) — não armazenada.
        builder.Ignore(b => b.Balance);

        builder.Property(b => b.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(b => new { b.MerchantId, b.Date })
            .IsUnique()
            .HasDatabaseName("ux_daily_balances_merchant_date");
    }
}
