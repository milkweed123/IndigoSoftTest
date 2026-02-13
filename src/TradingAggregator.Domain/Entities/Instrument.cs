using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Entities;

public class Instrument
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public ExchangeType Exchange { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
