namespace TradingAggregator.Application.DTOs;

/// <summary>
/// DTO for alert rule in API responses.
/// </summary>
public sealed class AlertRuleResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int InstrumentId { get; set; }
    public string? Symbol { get; set; }
    public string? Exchange { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public decimal Threshold { get; set; }
    public int? PeriodMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new alert rule.
/// </summary>
public sealed class CreateAlertRuleDto
{
    public string Name { get; init; } = string.Empty;
    public int InstrumentId { get; init; }
    public string RuleType { get; init; } = string.Empty;
    public decimal Threshold { get; init; }
    public int? PeriodMinutes { get; init; }
}

/// <summary>
/// DTO for updating an existing alert rule.
/// </summary>
public sealed class UpdateAlertRuleDto
{
    public string? Name { get; init; }
    public decimal? Threshold { get; init; }
    public int? PeriodMinutes { get; init; }
    public bool? IsActive { get; init; }
}
