using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Configurations;

public class TickConfiguration : IEntityTypeConfiguration<Tick>
{
    public void Configure(EntityTypeBuilder<Tick> builder)
    {
        builder.ToTable("ticks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(t => t.InstrumentId)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(t => t.Exchange)
            .HasColumnName("exchange")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<ExchangeType>(v))
            .IsRequired();

        builder.Property(t => t.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Price)
            .HasColumnName("price")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(t => t.Volume)
            .HasColumnName("volume")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(t => t.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(t => t.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();

        builder.Property(t => t.SourceType)
            .HasColumnName("source_type")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<DataSourceType>(v))
            .IsRequired();

        builder.HasOne(t => t.Instrument)
            .WithMany()
            .HasForeignKey(t => t.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.InstrumentId, t.Timestamp })
            .IsDescending(false, true);

        builder.HasIndex(t => t.Timestamp)
            .IsDescending(true);
    }
}
