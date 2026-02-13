using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Repositories;

public class AlertHistoryRepository : IAlertHistoryRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<AlertHistoryRepository> _logger;

    public AlertHistoryRepository(TradingDbContext context, ILogger<AlertHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(AlertHistory history, CancellationToken cancellationToken = default)
    {
        _context.AlertHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added alert history {HistoryId} for rule {RuleId}", history.Id, history.RuleId);
    }

    public async Task<IReadOnlyList<AlertHistory>> GetAsync(
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AlertHistories
            .AsNoTracking()
            .Include(h => h.Rule)
            .Include(h => h.Instrument)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(h => h.TriggeredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(h => h.TriggeredAt <= to.Value);

        return await query
            .OrderByDescending(h => h.TriggeredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
