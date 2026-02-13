using TradingAggregator.Application.Services;

namespace TradingAggregator.API.BackgroundServices;

/// <summary>
/// Background service that periodically flushes aggregated candles and buffered ticks
/// to persistent storage via <see cref="Application.Services.DataAggregationService"/>.
/// </summary>
public class DataAggregatorService : BackgroundService
{
    private readonly Application.Services.DataAggregationService _aggregationService;
    private readonly ILogger<DataAggregatorService> _logger;
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(10);

    public DataAggregatorService(
        Application.Services.DataAggregationService aggregationService,
        ILogger<DataAggregatorService> logger)
    {
        _aggregationService = aggregationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DataAggregatorService starting with flush interval of {Interval} seconds",
            _flushInterval.TotalSeconds);

        try
        {
            using var timer = new PeriodicTimer(_flushInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushDataAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("DataAggregatorService cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in DataAggregatorService");
            throw;
        }
        finally
        {
            try
            {
                _logger.LogInformation("Performing final flush before shutdown");
                await _aggregationService.FlushAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during final flush");
            }

            _logger.LogInformation("DataAggregatorService stopped");
        }
    }

    private async Task FlushDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _aggregationService.FlushAsync(cancellationToken);
            _logger.LogDebug("Successfully flushed data to storage");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing data to storage");
        }
    }
}
