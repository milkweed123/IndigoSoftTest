using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Configurations;

public class CandleConfiguration : IEntityTypeConfiguration<Candle>
{
    public void Configure(EntityTypeBuilder<Candle> builder)
    {
        builder.ToTable("candles");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.InstrumentId)
            .HasColumnName("instrument_id")
            .IsRequired();

        builder.Property(c => c.Interval)
            .HasColumnName("interval")
            .HasMaxLength(10)
            .HasConversion(
                v => v.ToShortString(),
                v => TimeIntervalExtensions.FromShortString(v))
            .IsRequired();

        builder.Property(c => c.OpenPrice)
            .HasColumnName("open_price")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(c => c.HighPrice)
            .HasColumnName("high_price")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(c => c.LowPrice)
            .HasColumnName("low_price")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(c => c.ClosePrice)
            .HasColumnName("close_price")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(c => c.Volume)
            .HasColumnName("volume")
            .HasPrecision(18, 8)
            .IsRequired();

        builder.Property(c => c.TradesCount)
            .HasColumnName("trades_count")
            .IsRequired();

        builder.Property(c => c.OpenTime)
            .HasColumnName("open_time")
            .IsRequired();

        builder.Property(c => c.CloseTime)
            .HasColumnName("close_time")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at");

        builder.HasOne(c => c.Instrument)
            .WithMany()
            .HasForeignKey(c => c.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.InstrumentId, c.Interval, c.OpenTime })
            .IsUnique();
    }
}
