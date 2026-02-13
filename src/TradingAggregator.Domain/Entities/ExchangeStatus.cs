using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Entities;

public class ExchangeStatus
{
    public int Id { get; set; }
    public ExchangeType Exchange { get; set; }
    public DataSourceType SourceType { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastTickAt { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
