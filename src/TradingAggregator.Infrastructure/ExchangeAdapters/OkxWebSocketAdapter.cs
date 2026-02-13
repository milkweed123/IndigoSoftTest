using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using Websocket.Client;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

public sealed class OkxWebSocketAdapter : BaseExchangeAdapter, IDisposable
{
    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<string> _okxInstrumentIds;
    private readonly Dictionary<string, string> _instrumentToSymbolMap;

    private WebsocketClient? _wsClient;
    private IDisposable? _messageSubscription;
    private IDisposable? _reconnectionSubscription;
    private IDisposable? _disconnectionSubscription;
    private CancellationTokenSource? _cts;

    public OkxWebSocketAdapter(
        ILogger<OkxWebSocketAdapter> logger,
        HttpClient httpClient,
        IReadOnlyList<string> symbols)
        : base(logger, httpClient)
    {
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
    public override DataSourceType SourceType => DataSourceType.WebSocket;
    public override IReadOnlyList<string> SupportedSymbols => _symbols;

    public override async Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            Logger.LogWarning("[OKX:WebSocket] Adapter is already running");
            return;
        }

        Logger.LogInformation("[OKX:WebSocket] Starting adapter for instruments: {Instruments}",
            string.Join(", ", _okxInstrumentIds));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var url = new Uri("wss://ws.okx.com:8443/ws/v5/public");

        _wsClient = new WebsocketClient(url)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(10)
        };

        _reconnectionSubscription = _wsClient.ReconnectionHappened.Subscribe(info =>
        {
            Logger.LogInformation(
                "[OKX:WebSocket] Reconnection happened, type: {Type}. Sending subscription.",
                info.Type);
            _isRunning = true;
            SendSubscription();
        });

        _disconnectionSubscription = _wsClient.DisconnectionHappened.Subscribe(info =>
        {
            if (info.Exception != null)
            {
                Logger.LogWarning(info.Exception,
                    "[OKX:WebSocket] Disconnection happened, type: {Type}",
                    info.Type);
                RecordError($"Disconnection: {info.Type} - {info.Exception.Message}");
            }
            else
            {
                Logger.LogInformation(
                    "[OKX:WebSocket] Disconnection happened, type: {Type}",
                    info.Type);
            }
        });

        _messageSubscription = _wsClient.MessageReceived.Subscribe(msg =>
        {
            _ = ProcessMessageAsync(msg.Text, writer, _cts.Token);
        });

        try
        {
            await _wsClient.Start();
            _isRunning = true;
            SendSubscription();
            Logger.LogInformation("[OKX:WebSocket] Connected and subscribed successfully");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            RecordError(ex.Message);
            Logger.LogError(ex, "[OKX:WebSocket] Failed to start WebSocket connection");
            throw;
        }
    }

    public override async Task StopAsync()
    {
        Logger.LogInformation("[OKX:WebSocket] Stopping adapter");

        _isRunning = false;

        _cts?.Cancel();

        _messageSubscription?.Dispose();
        _messageSubscription = null;

        _reconnectionSubscription?.Dispose();
        _reconnectionSubscription = null;

        _disconnectionSubscription?.Dispose();
        _disconnectionSubscription = null;

        if (_wsClient != null)
        {
            try
            {
                await _wsClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Shutting down");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[OKX:WebSocket] Error during WebSocket close");
            }

            _wsClient.Dispose();
            _wsClient = null;
        }

        _cts?.Dispose();
        _cts = null;

        Logger.LogInformation("[OKX:WebSocket] Adapter stopped");
    }

    private void SendSubscription()
    {
        if (_wsClient == null || !_wsClient.IsRunning)
            return;

        var args = _okxInstrumentIds.Select(id => new
        {
            channel = "trades",
            instId = id
        }).ToArray();

        var subscribeMsg = JsonSerializer.Serialize(new
        {
            op = "subscribe",
            args
        });

        _wsClient.Send(subscribeMsg);

        Logger.LogInformation("[OKX:WebSocket] Subscription sent: {Message}", subscribeMsg);
    }

    private async Task ProcessMessageAsync(string? message, ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(message) || ct.IsCancellationRequested)
            return;

        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("event", out _))
                return;

            if (!root.TryGetProperty("arg", out var argEl) ||
                !root.TryGetProperty("data", out var dataArray))
                return;

            if (!argEl.TryGetProperty("instId", out var instIdEl))
                return;

            var instId = instIdEl.GetString();
            if (instId == null || !_instrumentToSymbolMap.TryGetValue(instId, out var normalizedSymbol))
            {
                Logger.LogDebug("[OKX:WebSocket] Unknown instrument: {InstId}", instId);
                return;
            }

            foreach (var trade in dataArray.EnumerateArray())
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!trade.TryGetProperty("px", out var priceEl) ||
                    !trade.TryGetProperty("sz", out var sizeEl) ||
                    !trade.TryGetProperty("ts", out var timestampEl))
                    continue;

                var priceStr = priceEl.GetString();
                var sizeStr = sizeEl.GetString();
                var tsStr = timestampEl.GetString();

                if (priceStr == null || sizeStr == null || tsStr == null)
                    continue;

                if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price))
                    continue;

                if (!decimal.TryParse(sizeStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var volume))
                    continue;

                if (!long.TryParse(tsStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var timestampMs))
                    continue;

                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;

                var tick = new RawTick
                {
                    Exchange = ExchangeType.Okx,
                    SourceType = DataSourceType.WebSocket,
                    Symbol = normalizedSymbol,
                    Price = price,
                    Volume = volume,
                    Timestamp = timestamp
                };

                await WriteTickAsync(writer, tick, ct);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "[OKX:WebSocket] Failed to parse message: {Message}",
                message.Length > 200 ? message[..200] : message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RecordError(ex.Message);
            Logger.LogError(ex, "[OKX:WebSocket] Error processing message");
        }
    }

    public void Dispose()
    {
        _messageSubscription?.Dispose();
        _reconnectionSubscription?.Dispose();
        _disconnectionSubscription?.Dispose();
        _wsClient?.Dispose();
        _cts?.Dispose();
    }
}
