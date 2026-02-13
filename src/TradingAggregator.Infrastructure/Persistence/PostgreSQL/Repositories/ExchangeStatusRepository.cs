using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Repositories;

public class ExchangeStatusRepository : IExchangeStatusRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<ExchangeStatusRepository> _logger;

    public ExchangeStatusRepository(TradingDbContext context, ILogger<ExchangeStatusRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpsertAsync(ExchangeStatus status, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ExchangeStatuses
            .FirstOrDefaultAsync(e =>
                e.Exchange == status.Exchange &&
                e.SourceType == status.SourceType,
                cancellationToken);

        if (existing is not null)
        {
            existing.IsOnline = status.IsOnline;
            existing.LastTickAt = status.LastTickAt;
            existing.LastError = status.LastError;
            existing.UpdatedAt = DateTime.UtcNow;

            _logger.LogDebug(
                "Updated exchange status for {Exchange}/{SourceType}: IsOnline={IsOnline}",
                status.Exchange, status.SourceType, status.IsOnline);
        }
        else
        {
            status.UpdatedAt = DateTime.UtcNow;
            _context.ExchangeStatuses.Add(status);

            _logger.LogInformation(
                "Created exchange status for {Exchange}/{SourceType}: IsOnline={IsOnline}",
                status.Exchange, status.SourceType, status.IsOnline);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeStatus>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeStatuses
            .AsNoTracking()
            .OrderBy(e => e.Exchange)
            .ThenBy(e => e.SourceType)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExchangeStatus?> GetAsync(
        ExchangeType exchange,
        DataSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        return await _context.ExchangeStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.Exchange == exchange &&
                e.SourceType == sourceType,
                cancellationToken);
    }
}
