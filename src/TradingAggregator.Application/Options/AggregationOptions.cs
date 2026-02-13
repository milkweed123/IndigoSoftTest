using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Application.Options;

public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";

    /// <summary>
    /// Максимальное количество тиков в буфере перед сбросом в хранилище.
    /// </summary>
    public int TickBufferSize { get; set; } = 500;

    /// <summary>
    /// Интервал в секундах, через который буферизованные тики сбрасываются в хранилище.
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Временные интервалы для вычисления свечей.
    /// </summary>
    public List<TimeInterval> CandleIntervals { get; set; } =
    [
        TimeInterval.OneMinute,
        TimeInterval.FiveMinutes,
        TimeInterval.OneHour
    ];

    /// <summary>
    /// Максимальный возраст (в минутах) свечей в памяти перед вытеснением.
    /// </summary>
    public int InMemoryCandleRetentionMinutes { get; set; } = 120;
}
