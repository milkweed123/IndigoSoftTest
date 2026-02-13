using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

/// <summary>
/// REST API adapter for Bybit with periodic polling.
/// </summary>
public sealed class BybitRestAdapter : BaseExchangeAdapter
{
    private const string BaseUrl = "https://api.bybit.com";

    private readonly IReadOnlyList<string> _symbols;
    private readonly TimeSpan _pollingInterval;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public BybitRestAdapter(
        ILogger<BybitRestAdapter> logger,
        HttpClient httpClient,
        TimeSpan pollingInterval,
        IReadOnlyList<string> symbols)
        : base(logger, httpClient)
    {
        _pollingInterval = pollingInterval;
        _symbols = symbols;
    }

    public override ExchangeType Exchange => ExchangeType.Bybit;
    public override DataSourceType SourceType => DataSourceType.Rest;
    public override IReadOnlyList<string> SupportedSymbols => _symbols;

    public override Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            Logger.LogWarning("[Bybit:REST] Adapter is already running");
            return Task.CompletedTask;
        }

        Logger.LogInformation("[Bybit:REST] Starting adapter with {Interval}s interval for symbols: {_symbols}",
            _pollingInterval.TotalSeconds, string.Join(", ", _symbols));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        _pollingTask = Task.Run(() => PollLoopAsync(writer, _cts.Token), _cts.Token);

        Logger.LogInformation("[Bybit:REST] Adapter started");
        return Task.CompletedTask;
    }

    public override async Task StopAsync()
    {
        Logger.LogInformation("[Bybit:REST] Stopping adapter");

        _isRunning = false;

        if (_cts != null)
        {
            await _cts.CancelAsync();

            if (_pollingTask != null)
            {
                try
                {
                    await _pollingTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            _cts.Dispose();
            _cts = null;
        }

        _pollingTask = null;

        Logger.LogInformation("[Bybit:REST] Adapter stopped");
    }

    private async Task PollLoopAsync(ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        Logger.LogInformation("[Bybit:REST] Polling loop started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FetchAndWriteTicksAsync(writer, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordError(ex.Message);
                Logger.LogError(ex, "[Bybit:REST] Error during poll cycle");
            }

            try
            {
                await Task.Delay(_pollingInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Logger.LogInformation("[Bybit:REST] Polling loop ended");
    }

    private async Task FetchAndWriteTicksAsync(ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        foreach (var symbol in _symbols)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var url = $"{BaseUrl}/v5/market/tickers?category=spot&symbol={symbol}";
                var response = await HttpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("[Bybit:REST] Failed to fetch ticker for {Symbol}: {StatusCode}",
                        symbol, response.StatusCode);
                    RecordError($"HTTP {response.StatusCode} for {symbol}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                await ProcessTickerResponseAsync(json, symbol, writer, ct);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "[Bybit:REST] Network error fetching {Symbol}", symbol);
                RecordError($"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Bybit:REST] Error fetching {Symbol}", symbol);
                RecordError(ex.Message);
            }
        }
    }

    private async Task ProcessTickerResponseAsync(
        string json,
        string symbol,
        ChannelWriter<RawTick> writer,
        CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("list", out var list))
                return;

            foreach (var ticker in list.EnumerateArray())
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!ticker.TryGetProperty("lastPrice", out var priceEl) ||
                    !ticker.TryGetProperty("volume24h", out var volumeEl))
                    continue;

                var priceStr = priceEl.GetString();
                var volumeStr = volumeEl.GetString();

                if (priceStr == null || volumeStr == null)
                    continue;

                if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price))
                    continue;

                if (!decimal.TryParse(volumeStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var volume))
                    continue;

                var tick = new RawTick
                {
                    Exchange = ExchangeType.Bybit,
                    SourceType = DataSourceType.Rest,
                    Symbol = symbol,
                    Price = price,
                    Volume = volume,
                    Timestamp = DateTime.UtcNow
                };

                await WriteTickAsync(writer, tick, ct);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "[Bybit:REST] Failed to parse response for {Symbol}", symbol);
        }
    }
}
