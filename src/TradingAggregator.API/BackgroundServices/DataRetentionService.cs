using TradingAggregator.Infrastructure.Persistence.PostgreSQL;

namespace TradingAggregator.API.BackgroundServices;

/// <summary>
/// Фоновая служба для управления жизненным циклом партиций таблицы ticks.
/// - Создает партиции для будущих дат
/// - Удаляет старые партиции согласно retention policy
/// - Выполняется ежедневно
/// </summary>
public class DataRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataRetentionService> _logger;

    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);
    private readonly int _retentionDays;
    private readonly int _futurePartitionDays;

    public DataRetentionService(
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        ILogger<DataRetentionService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
        _logger = logger;

        _retentionDays = configuration.GetValue<int>("DataRetention:RetentionDays", 30);
        _futurePartitionDays = configuration.GetValue<int>("DataRetention:FuturePartitionDays", 7);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DataRetentionService starting with {RetentionDays} days retention, checking every {Interval} hours",
            _retentionDays,
            _checkInterval.TotalHours);

        // Wait 5 minutes after startup before first check
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(_checkInterval);

            // Execute immediately on first run
            await ExecuteMaintenanceAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteMaintenanceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("DataRetentionService cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in DataRetentionService");
            throw;
        }
        finally
        {
            _logger.LogInformation("DataRetentionService stopped");
        }
    }

    private async Task ExecuteMaintenanceAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting partition maintenance cycle");

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var connectionString = _configuration.GetConnectionString("PostgreSQL")
                ?? throw new InvalidOperationException("PostgreSQL connection string not found");

            var partitionManager = new PartitionManager(
                scope.ServiceProvider.GetRequiredService<ILogger<PartitionManager>>(),
                connectionString);

            // Create future partitions
            _logger.LogInformation("Creating future partitions for next {Days} days", _futurePartitionDays);
            await partitionManager.EnsureFuturePartitionsAsync(_futurePartitionDays, cancellationToken);

            // Drop old partitions
            _logger.LogInformation("Dropping partitions older than {Days} days", _retentionDays);
            await partitionManager.DropOldPartitionsAsync(_retentionDays, cancellationToken);

            // Log current partition status
            var partitions = await partitionManager.GetPartitionsAsync(cancellationToken);
            _logger.LogInformation(
                "Partition maintenance completed. Total partitions: {Count}",
                partitions.Count);

            if (partitions.Count > 0)
            {
                _logger.LogDebug(
                    "Oldest partition: {Oldest}, Newest partition: {Newest}",
                    partitions[^1].Name,
                    partitions[0].Name);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during partition maintenance");
        }
    }
}
