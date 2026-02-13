namespace TradingAggregator.Application.Options;

public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    /// <summary>
    /// Minimum interval between repeat notifications for one rule, in seconds.
    /// </summary>
    public int CooldownSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of concurrent notification sends.
    /// </summary>
    public int MaxConcurrentNotifications { get; set; } = 10;

    /// <summary>
    /// Configured notification channels.
    /// </summary>
    public List<NotificationChannelConfig> Channels { get; set; } = [];
}

public sealed class NotificationChannelConfig
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Channel settings (e.g., webhook URL, email address).
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
