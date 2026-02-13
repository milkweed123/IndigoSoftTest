using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Configurations;

public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("instruments");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(i => i.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.Exchange)
            .HasColumnName("exchange")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<ExchangeType>(v))
            .IsRequired();

        builder.Property(i => i.BaseCurrency)
            .HasColumnName("base_currency")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.QuoteCurrency)
            .HasColumnName("quote_currency")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at");

        builder.HasIndex(i => new { i.Symbol, i.Exchange })
            .IsUnique();
    }
}
