using FluxoCaixa.Consolidation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FluxoCaixa.Consolidation.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(m => m.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(m => m.EventId)
            .IsUnique()
            .HasDatabaseName("ux_processed_messages_event_id");
    }
}
