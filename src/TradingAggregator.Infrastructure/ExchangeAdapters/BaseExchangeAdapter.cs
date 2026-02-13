using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

public abstract class BaseExchangeAdapter : IExchangeAdapter
{
    protected readonly ILogger Logger;
    protected readonly HttpClient HttpClient;

    protected volatile bool _isRunning;
    protected DateTime? _lastTickTime;
    protected string? _lastError;
    private readonly object _statusLock = new();

    protected BaseExchangeAdapter(ILogger logger, HttpClient httpClient)
    {
        Logger = logger;
        HttpClient = httpClient;
    }

    public abstract ExchangeType Exchange { get; }
    public abstract DataSourceType SourceType { get; }
    public abstract IReadOnlyList<string> SupportedSymbols { get; }

    public abstract Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken);
    public abstract Task StopAsync();

    public Task<ExchangeStatus> GetStatusAsync()
    {
        lock (_statusLock)
        {
            var status = new ExchangeStatus
            {
                Exchange = Exchange,
                SourceType = SourceType,
                IsOnline = _isRunning,
                LastTickAt = _lastTickTime,
                LastError = _lastError,
                UpdatedAt = DateTime.UtcNow
            };

            return Task.FromResult(status);
        }
    }

    protected async Task WriteTickAsync(
        ChannelWriter<RawTick> writer,
        RawTick tick,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.WriteAsync(tick, cancellationToken);

            lock (_statusLock)
            {
                _lastTickTime = DateTime.UtcNow;
                _lastError = null;
            }

            Logger.LogDebug(
                "[{Exchange}:{SourceType}] Tick written: {Symbol} Price={Price} Volume={Volume}",
                Exchange, SourceType, tick.Symbol, tick.Price, tick.Volume);
        }
        catch (ChannelClosedException)
        {
            Logger.LogWarning(
                "[{Exchange}:{SourceType}] Channel is closed, cannot write tick for {Symbol}",
                Exchange, SourceType, tick.Symbol);
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation(
                "[{Exchange}:{SourceType}] Write cancelled for {Symbol}",
                Exchange, SourceType, tick.Symbol);
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _lastError = ex.Message;
            }

            Logger.LogError(ex,
                "[{Exchange}:{SourceType}] Error writing tick for {Symbol}",
                Exchange, SourceType, tick.Symbol);
        }
    }

    protected void RecordError(string error)
    {
        lock (_statusLock)
        {
            _lastError = error;
        }
    }
}
