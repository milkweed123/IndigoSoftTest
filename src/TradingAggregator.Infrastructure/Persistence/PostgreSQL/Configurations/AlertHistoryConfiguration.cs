using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Configurations;

public class AlertHistoryConfiguration : IEntityTypeConfiguration<AlertHistory>
{
    public void Configure(EntityTypeBuilder<AlertHistory> builder)
    {
        builder.ToTable("alert_histories");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.RuleId)
            .HasColumnName("rule_id")
            .IsRequired();

        builder.Property(a => a.InstrumentId)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(a => a.Message)
            .HasColumnName("message")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(a => a.TriggeredAt)
            .HasColumnName("triggered_at")
            .IsRequired();

        builder.HasOne(a => a.Rule)
            .WithMany()
            .HasForeignKey(a => a.RuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Instrument)
            .WithMany()
            .HasForeignKey(a => a.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.TriggeredAt)
            .IsDescending(true);
    }
}
