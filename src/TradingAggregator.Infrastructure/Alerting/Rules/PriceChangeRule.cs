using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting.Rules;

public sealed class PriceChangeRule : IAlertRuleEvaluator
{
    private readonly ILogger<PriceChangeRule> _logger;

    private readonly ConcurrentDictionary<string, (decimal FirstPrice, DateTime PeriodStart)> _periodStarts = new();

    private const int DefaultPeriodMinutes = 5;

    public PriceChangeRule(ILogger<PriceChangeRule> logger)
    {
        _logger = logger;
    }

    public bool CanEvaluate(AlertRule rule)
    {
        return rule.RuleType is AlertRuleType.PriceChangePercent;
    }

    public Task<AlertEvaluationResult> EvaluateAsync(
        AlertRule rule,
        NormalizedTick tick,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var periodMinutes = rule.PeriodMinutes ?? DefaultPeriodMinutes;
        var key = tick.Symbol;

        var entry = _periodStarts.GetOrAdd(key, _ => (tick.Price, tick.Timestamp));

        if (tick.Timestamp - entry.PeriodStart > TimeSpan.FromMinutes(periodMinutes))
        {
            entry = (tick.Price, tick.Timestamp);
            _periodStarts[key] = entry;
            return Task.FromResult(AlertEvaluationResult.NotTriggered());
        }

        if (entry.FirstPrice == 0m)
        {
            return Task.FromResult(AlertEvaluationResult.NotTriggered());
        }

        var changePercent = (tick.Price - entry.FirstPrice) / entry.FirstPrice * 100m;

        if (Math.Abs(changePercent) > rule.Threshold)
        {
            var message = $"Price change alert: {tick.Symbol} changed {changePercent:F2}% in {periodMinutes}min";
            _logger.LogInformation(message);
            return Task.FromResult(AlertEvaluationResult.Triggered(message));
        }

        return Task.FromResult(AlertEvaluationResult.NotTriggered());
    }
}
