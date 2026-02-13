using TradingAggregator.Application.DTOs;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Application.Services;

/// <summary>
/// Provides system statistics and status information by aggregating data from
/// <see cref="IMetricsCollector"/> and exchange status repositories.
/// </summary>
public sealed class MonitoringService
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly IExchangeStatusRepository _exchangeStatusRepository;
    private readonly ITickRepository _tickRepository;

    public MonitoringService(
        IMetricsCollector metricsCollector,
        IExchangeStatusRepository exchangeStatusRepository,
        ITickRepository tickRepository)
    {
        _metricsCollector = metricsCollector;
        _exchangeStatusRepository = exchangeStatusRepository;
        _tickRepository = tickRepository;
    }

    /// <summary>
    /// Creates a snapshot of current system statistics.
    /// </summary>
    public async Task<StatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var metrics = _metricsCollector.GetSnapshot();
        var exchangeStatuses = await _exchangeStatusRepository.GetAllAsync(cancellationToken);
        var totalTicksStored = await _tickRepository.GetCountAsync(cancellationToken);

        return new StatisticsDto
        {
            TotalTicksProcessed = metrics.TotalTicksProcessed,
            TotalTicksStored = totalTicksStored,
            TotalDuplicatesFiltered = metrics.TotalDuplicatesFiltered,
            CurrentQueueSize = metrics.CurrentQueueSize,
            UptimeSeconds = metrics.UptimeSeconds,
            SnapshotTime = metrics.SnapshotTime,
            TicksReceivedByExchange = metrics.TicksReceivedByExchange,
            AvgProcessingTimeMsByExchange = metrics.AvgProcessingTimeMsByExchange,
            ErrorsByExchange = metrics.ErrorsByExchange,
            ExchangeStatuses = exchangeStatuses
                .Select(s => new ExchangeStatusDto
                {
                    Exchange = s.Exchange.ToString(),
                    SourceType = s.SourceType.ToString(),
                    IsOnline = s.IsOnline,
                    LastTickAt = s.LastTickAt,
                    LastError = s.LastError,
                    UpdatedAt = s.UpdatedAt
                })
                .ToList()
        };
    }
}
