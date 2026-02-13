using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Entities;

public class NormalizedTick
{
    public ExchangeType Exchange { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Volume { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTime ReceivedAt { get; init; }
    public DataSourceType SourceType { get; init; }

    public string DeduplicationKey =>
        $"{Exchange}:{Symbol}:{Price}:{Volume}:{Timestamp:O}";
}
