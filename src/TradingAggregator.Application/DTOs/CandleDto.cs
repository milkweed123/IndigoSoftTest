namespace TradingAggregator.Application.DTOs;

/// <summary>
/// DTO for candles in API responses.
/// </summary>
public sealed class CandleDto
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public decimal OpenPrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal Volume { get; set; }
    public int TradesCount { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
}
