using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting.Rules;

public sealed class VolatilityRule : IAlertRuleEvaluator
{
    private readonly ILogger<VolatilityRule> _logger;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<(DateTime Timestamp, decimal Price)>> _priceWindows = new();

    private const int DefaultPeriodMinutes = 5;

    public VolatilityRule(ILogger<VolatilityRule> logger)
    {
        _logger = logger;
    }

    public bool CanEvaluate(AlertRule rule)
    {
        return rule.RuleType is AlertRuleType.Volatility;
    }

    public Task<AlertEvaluationResult> EvaluateAsync(
        AlertRule rule,
        NormalizedTick tick,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var periodMinutes = rule.PeriodMinutes ?? DefaultPeriodMinutes;
        var key = tick.Symbol;
        var cutoff = tick.Timestamp.AddMinutes(-periodMinutes);

        var queue = _priceWindows.GetOrAdd(key, _ => new ConcurrentQueue<(DateTime, decimal)>());
        queue.Enqueue((tick.Timestamp, tick.Price));

        while (queue.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            queue.TryDequeue(out _);
        }

        var entries = queue.ToArray();

        if (entries.Length < 3)
        {
            return Task.FromResult(AlertEvaluationResult.NotTriggered());
        }

        var returns = new double[entries.Length - 1];
        for (var i = 1; i < entries.Length; i++)
        {
            var prevPrice = (double)entries[i - 1].Price;
            var currPrice = (double)entries[i].Price;
            returns[i - 1] = prevPrice != 0.0 ? (currPrice - prevPrice) / prevPrice * 100.0 : 0.0;
        }

        var volatility = CalculateStandardDeviation(returns);

        if (volatility > (double)rule.Threshold)
        {
            var message = $"Volatility alert: {tick.Symbol} volatility is {volatility:F2}%";
            _logger.LogInformation(message);
            return Task.FromResult(AlertEvaluationResult.Triggered(message));
        }

        return Task.FromResult(AlertEvaluationResult.NotTriggered());
    }

    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length == 0)
            return 0.0;

        var mean = 0.0;
        foreach (var v in values)
            mean += v;
        mean /= values.Length;

        var sumSquaredDiffs = 0.0;
        foreach (var v in values)
        {
            var diff = v - mean;
            sumSquaredDiffs += diff * diff;
        }

        return Math.Sqrt(sumSquaredDiffs / values.Length);
    }
}
