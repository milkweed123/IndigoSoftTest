using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Application.Options;

public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";

    /// <summary>
    /// Maximum number of ticks in buffer before flushing to storage.
    /// </summary>
    public int TickBufferSize { get; set; } = 500;

    /// <summary>
    /// Interval in seconds at which buffered ticks are flushed to storage.
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Time intervals for calculating candles.
    /// </summary>
    public List<TimeInterval> CandleIntervals { get; set; } =
    [
        TimeInterval.OneMinute,
        TimeInterval.FiveMinutes,
        TimeInterval.OneHour
    ];

    /// <summary>
    /// Maximum age (in minutes) of candles in memory before eviction.
    /// </summary>
    public int InMemoryCandleRetentionMinutes { get; set; } = 120;
}
