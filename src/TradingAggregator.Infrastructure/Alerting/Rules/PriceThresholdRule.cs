using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting.Rules;

public sealed class PriceThresholdRule : IAlertRuleEvaluator
{
    private readonly ILogger<PriceThresholdRule> _logger;

    public PriceThresholdRule(ILogger<PriceThresholdRule> logger)
    {
        _logger = logger;
    }

    public bool CanEvaluate(AlertRule rule)
    {
        return rule.RuleType is AlertRuleType.PriceAbove or AlertRuleType.PriceBelow;
    }

    public Task<AlertEvaluationResult> EvaluateAsync(
        AlertRule rule,
        NormalizedTick tick,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var isTriggered = rule.RuleType switch
        {
            AlertRuleType.PriceAbove => tick.Price > rule.Threshold,
            AlertRuleType.PriceBelow => tick.Price < rule.Threshold,
            _ => false
        };

        if (isTriggered)
        {
            var message = $"Price alert: {tick.Symbol} on {tick.Exchange} is {tick.Price} (threshold: {rule.Threshold})";
            _logger.LogInformation(message);
            return Task.FromResult(AlertEvaluationResult.Triggered(message));
        }

        return Task.FromResult(AlertEvaluationResult.NotTriggered());
    }
}
