using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Фабрика для создания DbContext на этапе разработки (для миграций)
/// </summary>
public class TradingDbContextFactory : IDesignTimeDbContextFactory<TradingDbContext>
{
    public TradingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TradingDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=trading_aggregator;Username=postgres;Password=Sobaka123");

        return new TradingDbContext(optionsBuilder.Options);
    }
}
