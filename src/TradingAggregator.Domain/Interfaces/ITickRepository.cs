using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Interfaces;

public interface ITickRepository
{
    Task BulkInsertAsync(IEnumerable<Tick> ticks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tick>> GetAsync(
        string symbol,
        ExchangeType? exchange = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(CancellationToken cancellationToken = default);
}
