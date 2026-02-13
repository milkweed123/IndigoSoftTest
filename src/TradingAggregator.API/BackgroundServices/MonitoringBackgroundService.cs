using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.API.BackgroundServices;

/// <summary>
/// Фоновая служба, которая периодически записывает в журнал статистику производительности системы
/// и метрики для целей мониторинга и отладки.
/// </summary>
public class MonitoringBackgroundService : BackgroundService
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<MonitoringBackgroundService> _logger;
    private readonly TimeSpan _logInterval = TimeSpan.FromSeconds(30);

    public MonitoringBackgroundService(
        IMetricsCollector metricsCollector,
        ILogger<MonitoringBackgroundService> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MonitoringBackgroundService starting with log interval of {Interval} seconds",
            _logInterval.TotalSeconds);

        try
        {
            using var timer = new PeriodicTimer(_logInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                LogMetrics();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MonitoringBackgroundService cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in MonitoringBackgroundService");
            throw;
        }
        finally
        {
            _logger.LogInformation("MonitoringBackgroundService stopped");
        }
    }

    private void LogMetrics()
    {
        try
        {
            var snapshot = _metricsCollector.GetSnapshot();

            _logger.LogInformation(
                "Performance Metrics: " +
                "TotalTicks={TotalTicks}, " +
                "Duplicates={Duplicates}, " +
                "QueueSize={QueueSize}, " +
                "Uptime={Uptime}s",
                snapshot.TotalTicksProcessed,
                snapshot.TotalDuplicatesFiltered,
                snapshot.CurrentQueueSize,
                snapshot.UptimeSeconds);


            foreach (var (exchange, avgTime) in snapshot.AvgProcessingTimeMsByExchange)
            {
                var tickCount = snapshot.TicksProcessedByExchange.GetValueOrDefault(exchange, 0);
                var errors = snapshot.ErrorsByExchange.GetValueOrDefault(exchange, 0);

                _logger.LogInformation(
                    "Exchange {Exchange}: Ticks={Count}, AvgProcessingMs={AvgMs:F2}, Errors={Errors}",
                    exchange,
                    tickCount,
                    avgTime,
                    errors);
            }

            if (snapshot.CurrentQueueSize > 1000)
            {
                _logger.LogWarning(
                    "High queue size detected: {QueueSize} ticks pending",
                    snapshot.CurrentQueueSize);
            }

            foreach (var (exchange, avgTime) in snapshot.AvgProcessingTimeMsByExchange)
            {
                if (avgTime > 100)
                {
                    _logger.LogWarning(
                        "High average processing time for {Exchange}: {AvgMs:F2}ms",
                        exchange,
                        avgTime);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting or logging metrics");
        }
    }
}
