using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Interfaces;

public interface ICandleRepository
{
    Task UpsertAsync(Candle candle, CancellationToken cancellationToken = default);
    Task BulkUpsertAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Candle>> GetAsync(
        int instrumentId,
        TimeInterval interval,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
