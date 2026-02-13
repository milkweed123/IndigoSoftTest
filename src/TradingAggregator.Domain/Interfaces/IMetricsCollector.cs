using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Interfaces;

public interface IMetricsCollector
{
    void RecordTickReceived(ExchangeType exchange);
    void RecordTickProcessed(ExchangeType exchange, double processingTimeMs);
    void RecordTickStored(int count);
    void RecordDuplicateFiltered(ExchangeType exchange);
    void RecordPipelineQueueSize(int size);
    void RecordError(ExchangeType exchange, string errorType);
    PerformanceMetrics GetSnapshot();
}

public class PerformanceMetrics
{
    public Dictionary<string, long> TicksReceivedByExchange { get; init; } = new();
    public Dictionary<string, long> TicksProcessedByExchange { get; init; } = new();
    public Dictionary<string, double> AvgProcessingTimeMsByExchange { get; init; } = new();
    public long TotalTicksProcessed { get; init; }
    public long TotalTicksStored { get; init; }
    public long TotalDuplicatesFiltered { get; init; }
    public int CurrentQueueSize { get; init; }
    public Dictionary<string, long> ErrorsByExchange { get; init; } = new();
    public double UptimeSeconds { get; init; }
    public DateTime SnapshotTime { get; init; } = DateTime.UtcNow;
}
