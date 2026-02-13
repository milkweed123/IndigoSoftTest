using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

public sealed class BinanceRestAdapter : BaseExchangeAdapter
{
    private const string PriceEndpoint = "https://api.binance.com/api/v3/ticker/price";
    private const string Ticker24hEndpoint = "https://api.binance.com/api/v3/ticker/24hr";

    private readonly IReadOnlyList<string> _symbols;
    private readonly TimeSpan _pollingInterval;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public BinanceRestAdapter(
        ILogger<BinanceRestAdapter> logger,
        HttpClient httpClient,
        TimeSpan pollingInterval,
        IReadOnlyList<string> symbols)
        : base(logger, httpClient)
    {
        _pollingInterval = pollingInterval;
        _symbols = symbols;
    }

    public override ExchangeType Exchange => ExchangeType.Binance;
    public override DataSourceType SourceType => DataSourceType.Rest;
    public override IReadOnlyList<string> SupportedSymbols => _symbols;

    public override Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            Logger.LogWarning("[Binance:REST] Adapter is already running");
            return Task.CompletedTask;
        }

        Logger.LogInformation("[Binance:REST] Starting adapter with {Interval}s interval for symbols: {_symbols}",
            _pollingInterval.TotalSeconds, string.Join(", ", _symbols));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        _pollingTask = Task.Run(() => PollLoopAsync(writer, _cts.Token), _cts.Token);

        Logger.LogInformation("[Binance:REST] Adapter started");
        return Task.CompletedTask;
    }

    public override async Task StopAsync()
    {
        Logger.LogInformation("[Binance:REST] Stopping adapter");

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

        Logger.LogInformation("[Binance:REST] Adapter stopped");
    }

    private async Task PollLoopAsync(ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        Logger.LogInformation("[Binance:REST] Polling loop started");

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
                Logger.LogError(ex, "[Binance:REST] Error during poll cycle");
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

        Logger.LogInformation("[Binance:REST] Polling loop ended");
    }

    private async Task FetchAndWriteTicksAsync(ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        var pricesJson = await HttpClient.GetStringAsync(PriceEndpoint, ct);
        using var pricesDoc = JsonDocument.Parse(pricesJson);

        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in pricesDoc.RootElement.EnumerateArray())
        {
            var symbol = item.GetProperty("symbol").GetString();
            var priceStr = item.GetProperty("price").GetString();

            if (symbol != null && priceStr != null &&
                _symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase) &&
                decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price))
            {
                prices[symbol] = price;
            }
        }

        foreach (var symbol in _symbols)
        {
            if (ct.IsCancellationRequested)
                break;

            if (!prices.TryGetValue(symbol, out var price))
                continue;

            var volume = 0m;

            try
            {
                var ticker24hUrl = $"{Ticker24hEndpoint}?symbol={symbol}";
                var ticker24hJson = await HttpClient.GetStringAsync(ticker24hUrl, ct);
                using var ticker24hDoc = JsonDocument.Parse(ticker24hJson);
                var root = ticker24hDoc.RootElement;

                if (root.TryGetProperty("volume", out var volumeEl))
                {
                    var volumeStr = volumeEl.GetString();
                    if (volumeStr != null)
                    {
                        decimal.TryParse(volumeStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out volume);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex,
                    "[Binance:REST] Failed to fetch 24hr ticker for {Symbol}, using zero volume",
                    symbol);
            }

            var tick = new RawTick
            {
                Exchange = ExchangeType.Binance,
                SourceType = DataSourceType.Rest,
                Symbol = symbol,
                Price = price,
                Volume = volume,
                Timestamp = DateTime.UtcNow
            };

            await WriteTickAsync(writer, tick, ct);
        }

        Logger.LogDebug("[Binance:REST] Poll cycle completed, {Count} ticks written", prices.Count);
    }
}
