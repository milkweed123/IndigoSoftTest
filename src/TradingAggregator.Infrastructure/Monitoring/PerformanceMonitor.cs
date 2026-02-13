using System.Text;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Monitoring;

public sealed class PerformanceMonitor
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly IExchangeStatusRepository _exchangeStatusRepository;
    private readonly ILogger<PerformanceMonitor> _logger;

    public PerformanceMonitor(
        IMetricsCollector metricsCollector,
        IExchangeStatusRepository exchangeStatusRepository,
        ILogger<PerformanceMonitor> logger)
    {
        _metricsCollector = metricsCollector;
        _exchangeStatusRepository = exchangeStatusRepository;
        _logger = logger;
    }

    public string GetPerformanceReport()
    {
        var metrics = _metricsCollector.GetSnapshot();
        var sb = new StringBuilder();

        sb.AppendLine("========== Performance Report ==========");
        sb.AppendLine($"Snapshot Time: {metrics.SnapshotTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Uptime: {metrics.UptimeSeconds:F0} seconds");
        sb.AppendLine();

        sb.AppendLine("--- Ticks Received by Exchange ---");
        foreach (var kvp in metrics.TicksReceivedByExchange)
        {
            sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("--- Avg Processing Time (ms) by Exchange ---");
        foreach (var kvp in metrics.AvgProcessingTimeMsByExchange)
        {
            sb.AppendLine($"  {kvp.Key}: {kvp.Value:F2} ms");
        }

        sb.AppendLine();
        sb.AppendLine($"Total Ticks Processed: {metrics.TotalTicksProcessed}");
        sb.AppendLine($"Total Ticks Stored: {metrics.TotalTicksStored}");
        sb.AppendLine($"Total Duplicates Filtered: {metrics.TotalDuplicatesFiltered}");
        sb.AppendLine($"Current Queue Size: {metrics.CurrentQueueSize}");

        if (metrics.ErrorsByExchange.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- Errors by Exchange ---");
            foreach (var kvp in metrics.ErrorsByExchange)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        sb.AppendLine("=========================================");

        var report = sb.ToString();
        _logger.LogInformation("Performance report generated");

        return report;
    }

    public async Task<List<string>> DetectDataDelays(TimeSpan threshold)
    {
        var delayedExchanges = new List<string>();

        var exchangeStatuses = await _exchangeStatusRepository.GetAllAsync();

        foreach (var status in exchangeStatuses)
        {
            if (status.LastTickAt == null)
            {
                delayedExchanges.Add($"{status.Exchange}/{status.SourceType}");
                _logger.LogWarning("No ticks received yet for {Exchange}/{SourceType}",
                    status.Exchange, status.SourceType);
                continue;
            }

            var timeSinceLastTick = DateTime.UtcNow - status.LastTickAt.Value;

            if (timeSinceLastTick > threshold)
            {
                var exchangeName = $"{status.Exchange}/{status.SourceType}";
                delayedExchanges.Add(exchangeName);

                _logger.LogWarning(
                    "Data delay detected for {Exchange}: last tick was {Seconds:F0}s ago (threshold: {Threshold:F0}s)",
                    exchangeName,
                    timeSinceLastTick.TotalSeconds,
                    threshold.TotalSeconds);
            }
        }

        return delayedExchanges;
    }
}
