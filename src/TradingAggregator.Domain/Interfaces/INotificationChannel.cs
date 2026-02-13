namespace TradingAggregator.Domain.Interfaces;

public interface INotificationChannel
{
    string Name { get; }
    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
