using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using Websocket.Client;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

public sealed class BybitWebSocketAdapter : BaseExchangeAdapter, IDisposable
{
    private readonly IReadOnlyList<string> _symbols;

    private WebsocketClient? _wsClient;
    private IDisposable? _messageSubscription;
    private IDisposable? _reconnectionSubscription;
    private IDisposable? _disconnectionSubscription;
    private CancellationTokenSource? _cts;

    public BybitWebSocketAdapter(
        ILogger<BybitWebSocketAdapter> logger,
        HttpClient httpClient,
        IReadOnlyList<string> symbols)
        : base(logger, httpClient)
    {
        _symbols = symbols;
    }

    public override ExchangeType Exchange => ExchangeType.Bybit;
    public override DataSourceType SourceType => DataSourceType.WebSocket;
    public override IReadOnlyList<string> SupportedSymbols => _symbols;

    public override async Task StartAsync(ChannelWriter<RawTick> writer, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            Logger.LogWarning("[Bybit:WebSocket] Adapter is already running");
            return;
        }

        Logger.LogInformation("[Bybit:WebSocket] Starting adapter for symbols: {_symbols}",
            string.Join(", ", _symbols));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var url = new Uri("wss://stream.bybit.com/v5/public/spot");

        _wsClient = new WebsocketClient(url)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(10)
        };

        _reconnectionSubscription = _wsClient.ReconnectionHappened.Subscribe(info =>
        {
            Logger.LogInformation(
                "[Bybit:WebSocket] Reconnection happened, type: {Type}. Sending subscription.",
                info.Type);
            _isRunning = true;
            SendSubscription();
        });

        _disconnectionSubscription = _wsClient.DisconnectionHappened.Subscribe(info =>
        {
            if (info.Exception != null)
            {
                Logger.LogWarning(info.Exception,
                    "[Bybit:WebSocket] Disconnection happened, type: {Type}",
                    info.Type);
                RecordError($"Disconnection: {info.Type} - {info.Exception.Message}");
            }
            else
            {
                Logger.LogInformation(
                    "[Bybit:WebSocket] Disconnection happened, type: {Type}",
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
            Logger.LogInformation("[Bybit:WebSocket] Connected and subscribed successfully");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            RecordError(ex.Message);
            Logger.LogError(ex, "[Bybit:WebSocket] Failed to start WebSocket connection");
            throw;
        }
    }

    public override async Task StopAsync()
    {
        Logger.LogInformation("[Bybit:WebSocket] Stopping adapter");

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
                Logger.LogWarning(ex, "[Bybit:WebSocket] Error during WebSocket close");
            }

            _wsClient.Dispose();
            _wsClient = null;
        }

        _cts?.Dispose();
        _cts = null;

        Logger.LogInformation("[Bybit:WebSocket] Adapter stopped");
    }

    private void SendSubscription()
    {
        if (_wsClient == null || !_wsClient.IsRunning)
            return;

        var args = _symbols.Select(s => $"publicTrade.{s}").ToArray();

        var subscribeMsg = JsonSerializer.Serialize(new
        {
            op = "subscribe",
            args
        });

        _wsClient.Send(subscribeMsg);

        Logger.LogInformation("[Bybit:WebSocket] Subscription sent: {Message}", subscribeMsg);
    }

    private async Task ProcessMessageAsync(string? message, ChannelWriter<RawTick> writer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(message) || ct.IsCancellationRequested)
            return;

        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("op", out _))
                return;

            if (!root.TryGetProperty("topic", out var topicEl) ||
                !root.TryGetProperty("data", out var dataArray))
                return;

            var topic = topicEl.GetString();
            if (topic == null || !topic.StartsWith("publicTrade."))
                return;

            foreach (var trade in dataArray.EnumerateArray())
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!trade.TryGetProperty("s", out var symbolEl) ||
                    !trade.TryGetProperty("p", out var priceEl) ||
                    !trade.TryGetProperty("v", out var volumeEl) ||
                    !trade.TryGetProperty("T", out var timestampEl))
                    continue;

                var symbol = symbolEl.GetString();
                var priceStr = priceEl.GetString();
                var volumeStr = volumeEl.GetString();

                if (symbol == null || priceStr == null || volumeStr == null)
                    continue;

                if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price))
                    continue;

                if (!decimal.TryParse(volumeStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var volume))
                    continue;

                var timestampMs = timestampEl.GetInt64();
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;

                var tick = new RawTick
                {
                    Exchange = ExchangeType.Bybit,
                    SourceType = DataSourceType.WebSocket,
                    Symbol = symbol,
                    Price = price,
                    Volume = volume,
                    Timestamp = timestamp
                };

                await WriteTickAsync(writer, tick, ct);
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "[Bybit:WebSocket] Failed to parse message: {Message}",
                message.Length > 200 ? message[..200] : message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RecordError(ex.Message);
            Logger.LogError(ex, "[Bybit:WebSocket] Error processing message");
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
