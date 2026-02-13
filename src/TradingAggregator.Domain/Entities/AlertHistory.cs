namespace TradingAggregator.Domain.Entities;

public class AlertHistory
{
    public long Id { get; set; }
    public int RuleId { get; set; }
    public int InstrumentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    public AlertRule? Rule { get; set; }
    public Instrument? Instrument { get; set; }
}
