namespace TradingAggregator.Application.DTOs;

/// <summary>
/// DTO for system monitoring statistics.
/// </summary>
public sealed class StatisticsDto
{
    public long TotalTicksProcessed { get; init; }
    public long TotalTicksStored { get; init; }
    public long TotalDuplicatesFiltered { get; init; }
    public int CurrentQueueSize { get; init; }
    public double UptimeSeconds { get; init; }
    public DateTime SnapshotTime { get; init; }

    public Dictionary<string, long> TicksReceivedByExchange { get; init; } = new();
    public Dictionary<string, double> AvgProcessingTimeMsByExchange { get; init; } = new();
    public Dictionary<string, long> ErrorsByExchange { get; init; } = new();

    public List<ExchangeStatusDto> ExchangeStatuses { get; init; } = [];
}

/// <summary>
/// DTO for individual exchange connection status.
/// </summary>
public sealed class ExchangeStatusDto
{
    public string Exchange { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public bool IsOnline { get; init; }
    public DateTime? LastTickAt { get; init; }
    public string? LastError { get; init; }
    public DateTime UpdatedAt { get; init; }
}
