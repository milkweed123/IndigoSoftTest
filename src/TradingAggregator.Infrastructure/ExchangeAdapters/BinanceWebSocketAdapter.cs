using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using Websocket.Client;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

public sealed class BinanceWebSocketAdapter : BaseExchangeAdapter, IDisposable
{
    private readonly IReadOnlyList<string> _symbols;

    private WebsocketClient? _wsClient;
    private IDisposable? _messageSubscription;
    private IDisposable? _reconnectionSubscription;
    private IDisposable? _disconnectionSubscription;
    private CancellationTokenSource? _cts;

    public BinanceWebSocketAdapter(
        ILogger<BinanceWebSocketAdapter> logger,
        HttpClient httpClient,
        IReadOnlyList<string> symbols)
        : base(logger, httpClient)
    {
        _symbols = symbols;
    }

    public override ExchangeType Exchange => ExchangeType.Binance;
    public override DataSourceType SourceType => DataSourceType.WebSocket;
    public override IReadOnlyList<string> SupportedSymbols => _symbols;

    public override async Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            Logger.LogWarning("[Binance:WebSocket] Adapter is already running");
            return;
        }

        Logger.LogInformation("[Binance:WebSocket] Starting adapter for symbols: {Symbols}",
            string.Join(", ", _symbols));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var streams = string.Join("/", _symbols.Select(s => $"{s.ToLowerInvariant()}@trade"));
        var url = new Uri($"wss://stream.binance.com:9443/stream?streams={streams}");

        _wsClient = new WebsocketClient(url)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(10)
        };

        _reconnectionSubscription = _wsClient.ReconnectionHappened.Subscribe(info =>
        {
            Logger.LogInformation(
                "[Binance:WebSocket] Reconnection happened, type: {Type}",
                info.Type);
            _isRunning = true;
        });

        _disconnectionSubscription = _wsClient.DisconnectionHappened.Subscribe(info =>
        {
            if (info.Exception != null)
            {
                Logger.LogWarning(info.Exception,
                    "[Binance:WebSocket] Disconnection happened, type: {Type}",
                    info.Type);
                RecordError($"Disconnection: {info.Type} - {info.Exception.Message}");
            }
            else
            {
                Logger.LogInformation(
                    "[Binance:WebSocket] Disconnection happened, type: {Type}",
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
            Logger.LogInformation("[Binance:WebSocket] Connected successfully");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            RecordError(ex.Message);
            Logger.LogError(ex, "[Binance:WebSocket] Failed to start WebSocket connection");
            throw;
        }
    }

    public override async Task StopAsync()
    {
        Logger.LogInformation("[Binance:WebSocket] Stopping adapter");

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
                Logger.LogWarning(ex, "[Binance:WebSocket] Error during WebSocket close");
            }

            _wsClient.Dispose();
            _wsClient = null;
        }

        _cts?.Dispose();
        _cts = null;

        Logger.LogInformation("[Binance:WebSocket] Adapter stopped");
    }

    private async Task ProcessMessageAsync(string? message, ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(message) || ct.IsCancellationRequested)
            return;

        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("stream", out _) || !root.TryGetProperty("data", out var data))
                return;

            if (!data.TryGetProperty("s", out var symbolEl) ||
                !data.TryGetProperty("p", out var priceEl) ||
                !data.TryGetProperty("q", out var volumeEl) ||
                !data.TryGetProperty("T", out var timestampEl))
                return;

            var symbol = symbolEl.GetString();
            var priceStr = priceEl.GetString();
            var volumeStr = volumeEl.GetString();

            if (symbol == null || priceStr == null || volumeStr == null)
                return;

            if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price))
                return;

            if (!decimal.TryParse(volumeStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var volume))
                return;

            var timestampMs = timestampEl.GetInt64();
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;

            var tick = new RawTick
            {
                Exchange = ExchangeType.Binance,
                SourceType = DataSourceType.WebSocket,
                Symbol = symbol,
                Price = price,
                Volume = volume,
                Timestamp = timestamp
            };

            await WriteTickAsync(writer, tick, ct);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "[Binance:WebSocket] Failed to parse message: {Message}",
                message.Length > 200 ? message[..200] : message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RecordError(ex.Message);
            Logger.LogError(ex, "[Binance:WebSocket] Error processing message");
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
