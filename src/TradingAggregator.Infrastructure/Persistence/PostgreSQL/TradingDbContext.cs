using Microsoft.EntityFrameworkCore;
using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL;

public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Tick> Ticks => Set<Tick>();
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<ExchangeStatus> ExchangeStatuses => Set<ExchangeStatus>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertHistory> AlertHistories => Set<AlertHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradingDbContext).Assembly);
    }
}
