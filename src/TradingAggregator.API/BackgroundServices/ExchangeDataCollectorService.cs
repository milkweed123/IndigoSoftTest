using Microsoft.Extensions.Options;
using TradingAggregator.Application.Options;
using TradingAggregator.Application.Pipeline;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using TradingAggregator.Infrastructure.ExchangeAdapters;

namespace TradingAggregator.API.BackgroundServices;

/// <summary>
/// Background service that starts all configured exchange adapters,
/// initializes the tick processing pipeline, and monitors adapter status.
/// </summary>
public class ExchangeDataCollectorService : BackgroundService
{
    private readonly ITickPipeline _pipeline;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ExchangeAdapterFactory _adapterFactory;
    private readonly DataSourceOptions _dataSourceOptions;
    private readonly ILogger<ExchangeDataCollectorService> _logger;
    private readonly List<IExchangeAdapter> _adapters = new();

    public ExchangeDataCollectorService(
        ITickPipeline pipeline,
        IServiceScopeFactory serviceScopeFactory,
        ExchangeAdapterFactory adapterFactory,
        IOptions<DataSourceOptions> dataSourceOptions,
        ILogger<ExchangeDataCollectorService> logger)
    {
        _pipeline = pipeline;
        _serviceScopeFactory = serviceScopeFactory;
        _adapterFactory = adapterFactory;
        _dataSourceOptions = dataSourceOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExchangeDataCollectorService starting");

        try
        {
            await _pipeline.StartAsync(stoppingToken);
            _logger.LogInformation("Tick processing pipeline started");

            var adapters = _adapterFactory.CreateAdapters();
            _logger.LogInformation("Created {AdapterCount} exchange adapters", adapters.Count);

            await StartAllAdaptersAsync(adapters, stoppingToken);

            _logger.LogInformation(
                "Started {AdapterCount} exchange adapters",
                _adapters.Count);

            await MonitorAdaptersAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("ExchangeDataCollectorService cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in ExchangeDataCollectorService");
            throw;
        }
        finally
        {
            await StopAllAdaptersAsync();
            await _pipeline.StopAsync(CancellationToken.None);
            _logger.LogInformation("ExchangeDataCollectorService stopped");
        }
    }

    private async Task StartAllAdaptersAsync(
        IReadOnlyList<IExchangeAdapter> adapters,
        CancellationToken cancellationToken)
    {
        foreach (var adapter in adapters)
        {
            try
            {
                _logger.LogInformation(
                    "Starting {SourceType} adapter for {Exchange} with {SymbolCount} symbols",
                    adapter.SourceType,
                    adapter.Exchange,
                    adapter.SupportedSymbols.Count);

                await adapter.StartAsync(_pipeline.InputWriter, cancellationToken);

                _adapters.Add(adapter);

                await UpdateExchangeStatusAsync(
                    adapter.Exchange,
                    adapter.SourceType,
                    isOnline: true,
                    lastError: null,
                    cancellationToken);

                _logger.LogInformation(
                    "Started {SourceType} adapter for {Exchange}",
                    adapter.SourceType,
                    adapter.Exchange);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to start {SourceType} adapter for {Exchange}",
                    adapter.SourceType,
                    adapter.Exchange);

                await UpdateExchangeStatusAsync(
                    adapter.Exchange,
                    adapter.SourceType,
                    isOnline: false,
                    lastError: ex.Message,
                    cancellationToken);
            }
        }
    }

    private async Task MonitorAdaptersAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            foreach (var adapter in _adapters)
            {
                try
                {
                    var status = await adapter.GetStatusAsync();

                    await UpdateExchangeStatusAsync(
                        adapter.Exchange,
                        adapter.SourceType,
                        isOnline: status.IsOnline,
                        lastError: status.LastError,
                        cancellationToken);

                    if (!status.IsOnline)
                    {
                        _logger.LogWarning(
                            "Adapter {Exchange}-{SourceType} is not connected",
                            adapter.Exchange,
                            adapter.SourceType);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error checking health of adapter {Exchange}-{SourceType}",
                        adapter.Exchange,
                        adapter.SourceType);
                }
            }
        }
    }

    private async Task UpdateExchangeStatusAsync(
        ExchangeType exchange,
        DataSourceType sourceType,
        bool isOnline,
        string? lastError,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var exchangeStatusRepository = scope.ServiceProvider.GetRequiredService<IExchangeStatusRepository>();

            var status = new Domain.Entities.ExchangeStatus
            {
                Exchange = exchange,
                SourceType = sourceType,
                IsOnline = isOnline,
                LastError = lastError,
                UpdatedAt = DateTime.UtcNow
            };

            await exchangeStatusRepository.UpsertAsync(status, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update exchange status for {Exchange}-{SourceType}",
                exchange,
                sourceType);
        }
    }

    private async Task StopAllAdaptersAsync()
    {
        _logger.LogInformation("Stopping {Count} exchange adapters", _adapters.Count);

        var stopTasks = _adapters.Select(adapter =>
            Task.Run(async () =>
            {
                try
                {
                    await adapter.StopAsync();
                    _logger.LogInformation(
                        "Stopped adapter {Exchange}-{SourceType}",
                        adapter.Exchange,
                        adapter.SourceType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error stopping adapter {Exchange}-{SourceType}",
                        adapter.Exchange,
                        adapter.SourceType);
                }
            }));

        await Task.WhenAll(stopTasks);
    }
}
