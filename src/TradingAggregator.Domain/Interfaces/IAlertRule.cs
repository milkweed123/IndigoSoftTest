using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Domain.Interfaces;

public interface IAlertRuleEvaluator
{
    bool CanEvaluate(AlertRule rule);
    Task<AlertEvaluationResult> EvaluateAsync(AlertRule rule, NormalizedTick tick, CancellationToken cancellationToken = default);
}

public class AlertEvaluationResult
{
    public bool IsTriggered { get; init; }
    public string Message { get; init; } = string.Empty;

    public static AlertEvaluationResult NotTriggered() => new() { IsTriggered = false };
    public static AlertEvaluationResult Triggered(string message) => new() { IsTriggered = true, Message = message };
}
