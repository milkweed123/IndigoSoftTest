using System.Collections.Concurrent;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Monitoring;

public sealed class MetricsCollector : IMetricsCollector
{
    private readonly ConcurrentDictionary<ExchangeType, long> _ticksReceived = new();
    private readonly ConcurrentDictionary<ExchangeType, long> _ticksProcessedCount = new();
    private readonly ConcurrentDictionary<ExchangeType, double> _totalProcessingTimeMs = new();
    private readonly ConcurrentDictionary<ExchangeType, long> _duplicatesFiltered = new();
    private readonly ConcurrentDictionary<ExchangeType, long> _errors = new();

    private long _totalTicksStored;
    private int _currentQueueSize;

    private readonly DateTime _startTime;

    public MetricsCollector()
    {
        _startTime = DateTime.UtcNow;
    }

    public void RecordTickReceived(ExchangeType exchange)
    {
        _ticksReceived.AddOrUpdate(exchange, 1, static (_, count) => Interlocked.Increment(ref count));
    }

    public void RecordTickProcessed(ExchangeType exchange, double ms)
    {
        _ticksProcessedCount.AddOrUpdate(exchange, 1, static (_, count) => Interlocked.Increment(ref count));
        _totalProcessingTimeMs.AddOrUpdate(exchange, ms, (_, total) => total + ms);
    }

    public void RecordTickStored(int count)
    {
        Interlocked.Add(ref _totalTicksStored, count);
    }

    public void RecordDuplicateFiltered(ExchangeType exchange)
    {
        _duplicatesFiltered.AddOrUpdate(exchange, 1, static (_, count) => Interlocked.Increment(ref count));
    }

    public void RecordPipelineQueueSize(int size)
    {
        Interlocked.Exchange(ref _currentQueueSize, size);
    }

    public void RecordError(ExchangeType exchange, string errorType)
    {
        _errors.AddOrUpdate(exchange, 1, static (_, count) => Interlocked.Increment(ref count));
    }

    public PerformanceMetrics GetSnapshot()
    {
        var ticksReceivedByExchange = new Dictionary<string, long>();
        foreach (var kvp in _ticksReceived)
        {
            ticksReceivedByExchange[kvp.Key.ToString()] = kvp.Value;
        }

        var ticksProcessedByExchange = new Dictionary<string, long>();
        foreach (var kvp in _ticksProcessedCount)
        {
            ticksProcessedByExchange[kvp.Key.ToString()] = kvp.Value;
        }

        var avgProcessingTimeMsByExchange = new Dictionary<string, double>();
        foreach (var kvp in _ticksProcessedCount)
        {
            var exchange = kvp.Key;
            var count = kvp.Value;
            if (count > 0 && _totalProcessingTimeMs.TryGetValue(exchange, out var totalMs))
            {
                avgProcessingTimeMsByExchange[exchange.ToString()] = totalMs / count;
            }
            else
            {
                avgProcessingTimeMsByExchange[exchange.ToString()] = 0.0;
            }
        }

        var totalTicksProcessed = 0L;
        foreach (var kvp in _ticksProcessedCount)
        {
            totalTicksProcessed += kvp.Value;
        }

        var totalDuplicatesFiltered = 0L;
        foreach (var kvp in _duplicatesFiltered)
        {
            totalDuplicatesFiltered += kvp.Value;
        }

        var errorsByExchange = new Dictionary<string, long>();
        foreach (var kvp in _errors)
        {
            errorsByExchange[kvp.Key.ToString()] = kvp.Value;
        }

        return new PerformanceMetrics
        {
            TicksReceivedByExchange = ticksReceivedByExchange,
            TicksProcessedByExchange = ticksProcessedByExchange,
            AvgProcessingTimeMsByExchange = avgProcessingTimeMsByExchange,
            TotalTicksProcessed = totalTicksProcessed,
            TotalTicksStored = Interlocked.Read(ref _totalTicksStored),
            TotalDuplicatesFiltered = totalDuplicatesFiltered,
            CurrentQueueSize = Interlocked.CompareExchange(ref _currentQueueSize, 0, 0),
            ErrorsByExchange = errorsByExchange,
            UptimeSeconds = (DateTime.UtcNow - _startTime).TotalSeconds,
            SnapshotTime = DateTime.UtcNow
        };
    }
}
