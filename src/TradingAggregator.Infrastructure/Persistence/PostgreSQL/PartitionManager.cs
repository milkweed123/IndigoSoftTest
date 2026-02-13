using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Управляет партициями таблицы ticks в PostgreSQL.
/// Автоматически создает новые партиции и удаляет старые согласно retention policy.
/// </summary>
public class PartitionManager
{
    private readonly ILogger<PartitionManager> _logger;
    private readonly string _connectionString;

    public PartitionManager(ILogger<PartitionManager> logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    /// <summary>
    /// Применяет партиционирование к таблице ticks.
    /// ВНИМАНИЕ: Эта операция может занять время на больших таблицах.
    /// </summary>
    public async Task ApplyPartitioningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying partitioning to ticks table...");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Persistence",
            "PostgreSQL",
            "Migrations",
            "AddTicksPartitioning.sql");

        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("Partitioning script not found at {Path}", scriptPath);
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 300; // 5 minutes for large tables

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Successfully applied partitioning to ticks table");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying partitioning");
            throw;
        }
    }

    /// <summary>
    /// Создает партиции для будущих дат (next N days).
    /// </summary>
    public async Task EnsureFuturePartitionsAsync(
        int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Ensuring future partitions exist for next {Days} days", daysAhead);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT ensure_future_tick_partitions(@days_ahead)",
            connection);
        command.Parameters.AddWithValue("days_ahead", daysAhead);

        try
        {
            var created = (int?)await command.ExecuteScalarAsync(cancellationToken) ?? 0;

            if (created > 0)
            {
                _logger.LogInformation("Created {Count} future partition(s)", created);
            }
            else
            {
                _logger.LogDebug("All future partitions already exist");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating future partitions");
            throw;
        }
    }

    /// <summary>
    /// Удаляет старые партиции согласно retention policy.
    /// </summary>
    public async Task DropOldPartitionsAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Dropping tick partitions older than {Days} days", retentionDays);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT drop_old_tick_partitions(@retention_days)",
            connection);
        command.Parameters.AddWithValue("retention_days", retentionDays);

        try
        {
            var dropped = (int?)await command.ExecuteScalarAsync(cancellationToken) ?? 0;

            if (dropped > 0)
            {
                _logger.LogInformation("Dropped {Count} old partition(s)", dropped);
            }
            else
            {
                _logger.LogDebug("No old partitions to drop");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dropping old partitions");
            throw;
        }
    }

    /// <summary>
    /// Возвращает список всех партиций таблицы ticks.
    /// </summary>
    public async Task<IReadOnlyList<PartitionInfo>> GetPartitionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT 
                schemaname,
                tablename,
                pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
            FROM pg_tables
            WHERE tablename LIKE 'ticks_20%'
            ORDER BY tablename DESC";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var partitions = new List<PartitionInfo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            partitions.Add(new PartitionInfo
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                Size = reader.GetString(2)
            });
        }

        return partitions.AsReadOnly();
    }
}

public record PartitionInfo
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required string Size { get; init; }
}
