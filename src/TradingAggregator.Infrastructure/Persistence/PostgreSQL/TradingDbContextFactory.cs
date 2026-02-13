using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Factory for creating DbContext at design time (for migrations)
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
