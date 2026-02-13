using System.Threading.Channels;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Interfaces;

public interface IExchangeAdapter
{
    ExchangeType Exchange { get; }
    DataSourceType SourceType { get; }
    IReadOnlyList<string> SupportedSymbols { get; }

    Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken);
    Task StopAsync();
    Task<ExchangeStatus> GetStatusAsync();
}

public class RawTick
{
    public ExchangeType Exchange { get; init; }
    public DataSourceType SourceType { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Volume { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}
