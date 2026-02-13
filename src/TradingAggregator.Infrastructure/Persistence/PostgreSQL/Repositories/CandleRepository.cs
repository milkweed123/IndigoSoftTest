using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Repositories;

public class CandleRepository : ICandleRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<CandleRepository> _logger;

    public CandleRepository(TradingDbContext context, ILogger<CandleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpsertAsync(Candle candle, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Candles
            .FirstOrDefaultAsync(c =>
                c.InstrumentId == candle.InstrumentId &&
                c.Interval == candle.Interval &&
                c.OpenTime == candle.OpenTime,
                cancellationToken);

        if (existing is not null)
        {
            existing.OpenPrice = candle.OpenPrice;
            existing.HighPrice = candle.HighPrice;
            existing.LowPrice = candle.LowPrice;
            existing.ClosePrice = candle.ClosePrice;
            existing.Volume = candle.Volume;
            existing.TradesCount = candle.TradesCount;
            existing.CloseTime = candle.CloseTime;

            _logger.LogDebug(
                "Updated candle for InstrumentId={InstrumentId}, Interval={Interval}, OpenTime={OpenTime}",
                candle.InstrumentId, candle.Interval, candle.OpenTime);
        }
        else
        {
            _context.Candles.Add(candle);

            _logger.LogDebug(
                "Inserted candle for InstrumentId={InstrumentId}, Interval={Interval}, OpenTime={OpenTime}",
                candle.InstrumentId, candle.Interval, candle.OpenTime);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default)
    {
        var candleList = candles.ToList();

        if (candleList.Count == 0)
            return;

        // ВАЖНО: EFCore.BulkExtensions НЕ поддерживает HasConversion для enum -> string
        // Поэтому используем стандартный EF Core метод, который корректно применяет конвертацию
        // Interval конвертируется: OneMinute -> "1m", FiveMinutes -> "5m", OneHour -> "1h"

        _logger.LogDebug("Upserting {Count} candles (using EF Core with HasConversion)", candleList.Count);

        foreach (var candle in candleList)
        {
            var existing = await _context.Candles
                .FirstOrDefaultAsync(c =>
                    c.InstrumentId == candle.InstrumentId &&
                    c.Interval == candle.Interval &&
                    c.OpenTime == candle.OpenTime,
                    cancellationToken);

            if (existing is not null)
            {
                existing.OpenPrice = candle.OpenPrice;
                existing.HighPrice = candle.HighPrice;
                existing.LowPrice = candle.LowPrice;
                existing.ClosePrice = candle.ClosePrice;
                existing.Volume = candle.Volume;
                existing.TradesCount = candle.TradesCount;
                existing.CloseTime = candle.CloseTime;
            }
            else
            {
                _context.Candles.Add(candle);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Upserted {Count} candles successfully", candleList.Count);
    }

    public async Task<IReadOnlyList<Candle>> GetAsync(
        int instrumentId,
        TimeInterval interval,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Candles
            .AsNoTracking()
            .Where(c => c.InstrumentId == instrumentId && c.Interval == interval);

        if (from.HasValue)
            query = query.Where(c => c.OpenTime >= from.Value);

        if (to.HasValue)
            query = query.Where(c => c.OpenTime <= to.Value);

        return await query
            .OrderByDescending(c => c.OpenTime)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
