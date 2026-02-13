using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting.Rules;

public sealed class VolumeChangeRule : IAlertRuleEvaluator
{
    private readonly ILogger<VolumeChangeRule> _logger;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<(DateTime Timestamp, decimal Volume)>> _volumeWindows = new();

    private const int DefaultPeriodMinutes = 5;

    public VolumeChangeRule(ILogger<VolumeChangeRule> logger)
    {
        _logger = logger;
    }

    public bool CanEvaluate(AlertRule rule)
    {
        return rule.RuleType is AlertRuleType.VolumeSpike;
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

        var queue = _volumeWindows.GetOrAdd(key, _ => new ConcurrentQueue<(DateTime, decimal)>());
        queue.Enqueue((tick.Timestamp, tick.Volume));

        while (queue.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
        {
            queue.TryDequeue(out _);
        }

        var entries = queue.ToArray();

        if (entries.Length < 2)
        {
            return Task.FromResult(AlertEvaluationResult.NotTriggered());
        }

        var historicalEntries = entries.AsSpan(0, entries.Length - 1);
        var sum = 0m;
        foreach (var entry in historicalEntries)
        {
            sum += entry.Volume;
        }

        var averageVolume = sum / historicalEntries.Length;

        if (averageVolume == 0m)
        {
            return Task.FromResult(AlertEvaluationResult.NotTriggered());
        }

        var ratio = tick.Volume / averageVolume;

        if (ratio > rule.Threshold)
        {
            var message = $"Volume spike: {tick.Symbol} volume {tick.Volume} is {ratio:F2}x average";
            _logger.LogInformation(message);
            return Task.FromResult(AlertEvaluationResult.Triggered(message));
        }

        return Task.FromResult(AlertEvaluationResult.NotTriggered());
    }
}
