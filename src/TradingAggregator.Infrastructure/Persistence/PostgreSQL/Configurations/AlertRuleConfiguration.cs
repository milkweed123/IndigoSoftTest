using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Configurations;

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("alert_rules");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.InstrumentId)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(a => a.RuleType)
            .HasColumnName("rule_type")
            .HasMaxLength(30)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<AlertRuleType>(v))
            .IsRequired();

        builder.Property(a => a.Threshold)
            .HasColumnName("threshold")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(a => a.PeriodMinutes)
            .HasColumnName("period_minutes");

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.HasOne(a => a.Instrument)
            .WithMany()
            .HasForeignKey(a => a.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
