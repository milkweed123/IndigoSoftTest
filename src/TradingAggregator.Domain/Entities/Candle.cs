using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Entities;

public class Candle
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public TimeInterval Interval { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal Volume { get; set; }
    public int TradesCount { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Instrument? Instrument { get; set; }

    public void ApplyTick(decimal price, decimal volume)
    {
        if (TradesCount == 0)
            OpenPrice = price;

        HighPrice = Math.Max(HighPrice, price);
        LowPrice = LowPrice == 0 ? price : Math.Min(LowPrice, price);
        ClosePrice = price;
        Volume += volume;
        TradesCount++;
    }
}
