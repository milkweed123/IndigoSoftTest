using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Entities;

public class Tick
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public ExchangeType Exchange { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DataSourceType SourceType { get; set; }

    public Instrument? Instrument { get; set; }
}
