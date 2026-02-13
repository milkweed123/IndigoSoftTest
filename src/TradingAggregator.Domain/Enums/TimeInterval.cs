namespace TradingAggregator.Domain.Enums;

public enum TimeInterval
{
    OneMinute,
    FiveMinutes,
    OneHour
}

public static class TimeIntervalExtensions
{
    public static TimeSpan ToTimeSpan(this TimeInterval interval) => interval switch
    {
        TimeInterval.OneMinute => TimeSpan.FromMinutes(1),
        TimeInterval.FiveMinutes => TimeSpan.FromMinutes(5),
        TimeInterval.OneHour => TimeSpan.FromHours(1),
        _ => throw new ArgumentOutOfRangeException(nameof(interval))
    };

    public static string ToShortString(this TimeInterval interval) => interval switch
    {
        TimeInterval.OneMinute => "1m",
        TimeInterval.FiveMinutes => "5m",
        TimeInterval.OneHour => "1h",
        _ => throw new ArgumentOutOfRangeException(nameof(interval))
    };

    public static TimeInterval FromShortString(string value) => value switch
    {
        "1m" => TimeInterval.OneMinute,
        "5m" => TimeInterval.FiveMinutes,
        "1h" => TimeInterval.OneHour,
        _ => throw new ArgumentOutOfRangeException(nameof(value), $"Unknown interval: {value}")
    };
}
