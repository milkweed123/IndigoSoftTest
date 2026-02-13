namespace TradingAggregator.Application.Options;

public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    /// <summary>
    /// Минимальный интервал между повторными уведомлениями для одного правила, в секундах.
    /// </summary>
    public int CooldownSeconds { get; set; } = 300;

    /// <summary>
    /// Максимальное количество одновременных отправок уведомлений.
    /// </summary>
    public int MaxConcurrentNotifications { get; set; } = 10;

    /// <summary>
    /// Настроенные каналы уведомлений.
    /// </summary>
    public List<NotificationChannelConfig> Channels { get; set; } = [];
}

public sealed class NotificationChannelConfig
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Настройки канала (например, URL вебхука, email-адрес).
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
