using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Configurations;

public class ExchangeStatusConfiguration : IEntityTypeConfiguration<ExchangeStatus>
{
    public void Configure(EntityTypeBuilder<ExchangeStatus> builder)
    {
        builder.ToTable("exchange_statuses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Exchange)
            .HasColumnName("exchange")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<ExchangeType>(v))
            .IsRequired();

        builder.Property(e => e.SourceType)
            .HasColumnName("source_type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<DataSourceType>(v))
            .IsRequired();

        builder.Property(e => e.IsOnline)
            .HasColumnName("is_online")
            .IsRequired();

        builder.Property(e => e.LastTickAt)
            .HasColumnName("last_tick_at");

        builder.Property(e => e.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(1000);

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(e => new { e.Exchange, e.SourceType })
            .IsUnique();
    }
}
