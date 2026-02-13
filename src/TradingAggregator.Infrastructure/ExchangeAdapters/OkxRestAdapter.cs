using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

/// <summary>
/// REST API адаптер для OKX с периодическим polling.
/// </summary>
public sealed class OkxRestAdapter : BaseExchangeAdapter
{
    private const string BaseUrl = "https://www.okx.com";

    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<string> _okxInstrumentIds;
    private readonly Dictionary<string, string> _instrumentToSymbolMap;
    private readonly TimeSpan _pollingInterval;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public OkxRestAdapter(
        ILogger<OkxRestAdapter> logger,
        HttpClient httpClient,
        TimeSpan pollingInterval,
        IReadOnlyList<string> symbols)
        : base(logger, httpClient)
    {
        _pollingInterval = pollingInterval;
        _symbols = symbols;

        // Convert BTCUSDT -> BTC-USDT for OKX API
        _okxInstrumentIds = symbols
            .Select(ConvertToOkxInstrumentId)
            .ToList()
            .AsReadOnly();

        // Create mapping: BTC-USDT -> BTCUSDT
        _instrumentToSymbolMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < symbols.Count; i++)
        {
            _instrumentToSymbolMap[_okxInstrumentIds[i]] = symbols[i];
        }
    }

    private static string ConvertToOkxInstrumentId(string normalizedSymbol)
    {
        // BTCUSDT -> BTC-USDT, ETHUSDT -> ETH-USDT
        if (normalizedSymbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
        {
            var baseCurrency = normalizedSymbol.Substring(0, normalizedSymbol.Length - 4);
            return $"{baseCurrency}-USDT";
        }

        return normalizedSymbol;
    }

    public override ExchangeType Exchange => ExchangeType.Okx;
    public override DataSourceType SourceType => DataSourceType.Rest;
    public override IReadOnlyList<string> SupportedSymbols => _symbols;

    public override Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            Logger.LogWarning("[OKX:REST] Adapter is already running");
            return Task.CompletedTask;
        }

        Logger.LogInformation("[OKX:REST] Starting adapter with {Interval}s interval for instruments: {Instruments}",
            _pollingInterval.TotalSeconds, string.Join(", ", _okxInstrumentIds));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        _pollingTask = Task.Run(() => PollLoopAsync(writer, _cts.Token), _cts.Token);

        Logger.LogInformation("[OKX:REST] Adapter started");
        return Task.CompletedTask;
    }

    public override async Task StopAsync()
    {
        Logger.LogInformation("[OKX:REST] Stopping adapter");

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

        Logger.LogInformation("[OKX:REST] Adapter stopped");
    }

    private async Task PollLoopAsync(ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        Logger.LogInformation("[OKX:REST] Polling loop started");

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
                Logger.LogError(ex, "[OKX:REST] Error during poll cycle");
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

        Logger.LogInformation("[OKX:REST] Polling loop ended");
    }

    private async Task FetchAndWriteTicksAsync(ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        foreach (var instId in _okxInstrumentIds)
        {
            if (ct.IsCancellationRequested)
                break;

            if (!_instrumentToSymbolMap.TryGetValue(instId, out var symbol))
                continue;

            try
            {
                var url = $"{BaseUrl}/api/v5/market/ticker?instId={instId}";
                var response = await HttpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("[OKX:REST] Failed to fetch ticker for {InstId}: {StatusCode}",
                        instId, response.StatusCode);
                    RecordError($"HTTP {response.StatusCode} for {instId}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                await ProcessTickerResponseAsync(json, symbol, writer, ct);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "[OKX:REST] Network error fetching {InstId}", instId);
                RecordError($"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[OKX:REST] Error fetching {InstId}", instId);
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

            if (!root.TryGetProperty("data", out var dataArray))
                return;

            foreach (var ticker in dataArray.EnumerateArray())
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!ticker.TryGetProperty("last", out var priceEl) ||
                    !ticker.TryGetProperty("vol24h", out var volumeEl))
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
                    Exchange = ExchangeType.Okx,
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
            Logger.LogWarning(ex, "[OKX:REST] Failed to parse response for {Symbol}", symbol);
        }
    }
}
