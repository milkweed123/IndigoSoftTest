namespace TradingAggregator.Application.DTOs;

/// <summary>
/// DTO for tick data in API responses.
/// </summary>
public sealed class TickDto
{
    public string Exchange { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Volume { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTime ReceivedAt { get; init; }
    public string SourceType { get; init; } = string.Empty;
}
