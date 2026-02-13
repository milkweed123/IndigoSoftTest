using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Entities;

public class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int InstrumentId { get; set; }
    public AlertRuleType RuleType { get; set; }
    public decimal Threshold { get; set; }
    public int? PeriodMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Instrument? Instrument { get; set; }
}
