using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting.Channels;

public sealed class ConsoleNotifier : INotificationChannel
{
    private readonly ILogger<ConsoleNotifier> _logger;

    public string Name => "Console";

    public ConsoleNotifier(ILogger<ConsoleNotifier> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var previousColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ALERT: {message}");
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }

        _logger.LogDebug("Console notification sent: {Message}", message);

        return Task.CompletedTask;
    }
}
