using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Repositories;

public class TickRepository : ITickRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<TickRepository> _logger;

    public TickRepository(TradingDbContext context, ILogger<TickRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task BulkInsertAsync(IEnumerable<Tick> ticks, CancellationToken cancellationToken = default)
    {
        var tickList = ticks.ToList();

        if (tickList.Count == 0)
            return;

        var bulkConfig = new BulkConfig
        {
            BatchSize = 5000,
            BulkCopyTimeout = 30,
            EnableStreaming = true,
            UseTempDB = true
        };

        await _context.BulkInsertAsync(tickList, bulkConfig, cancellationToken: cancellationToken);

        _logger.LogDebug("Bulk inserted {Count} ticks using optimized batch insert (up to 50x faster)", tickList.Count);
    }

    public async Task<IReadOnlyList<Tick>> GetAsync(
        string symbol,
        ExchangeType? exchange = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Ticks
            .AsNoTracking()
            .Where(t => t.Symbol == symbol);

        if (exchange.HasValue)
            query = query.Where(t => t.Exchange == exchange.Value);

        if (from.HasValue)
            query = query.Where(t => t.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.Timestamp <= to.Value);

        return await query
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Ticks.LongCountAsync(cancellationToken);
    }
}
