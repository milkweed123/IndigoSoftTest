using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting.Channels;

public sealed class EmailNotifierStub : INotificationChannel
{
    private readonly ILogger<EmailNotifierStub> _logger;

    public string Name => "Email";

    public EmailNotifierStub(ILogger<EmailNotifierStub> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _logger.LogInformation("Email notification (stub): {Message}", message);

        return Task.CompletedTask;
    }
}
