using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingAggregator.Application.Options;
using TradingAggregator.Application.Pipeline;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Application.Services;

/// <summary>
/// Aggregates incoming ticks into OHLCV candles and periodically flushes
/// completed candles to <see cref="ICandleRepository"/>.
/// Raw ticks are buffered and inserted in batches via <see cref="ITickRepository"/>.
/// </summary>
public sealed class DataAggregationService : ITickPipelineHandler, IAsyncDisposable
{
    /// <summary>
    /// Composite key for in-memory candle dictionary.
    /// </summary>
    private readonly record struct CandleKey(int InstrumentId, TimeInterval Interval, DateTime OpenTime);

    /// <summary>
    /// Composite key for instrument cache.
    /// </summary>
    private readonly record struct InstrumentKey(string Symbol, ExchangeType Exchange);

    private readonly ConcurrentDictionary<CandleKey, Candle> _candles = new();
    private readonly ConcurrentQueue<Tick> _tickBuffer = new();

    // In-memory кэш инструментов для избежания 300+ запросов/сек к БД
    private readonly ConcurrentDictionary<InstrumentKey, Instrument> _instrumentCache = new();

    private readonly ICandleRepository _candleRepository;
    private readonly ITickRepository _tickRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly AggregationOptions _options;
    private readonly ILogger<DataAggregationService> _logger;

    private Timer? _flushTimer;
    private int _isFlushing;

    public DataAggregationService(
        ICandleRepository candleRepository,
        ITickRepository tickRepository,
        IInstrumentRepository instrumentRepository,
        IOptions<AggregationOptions> options,
        ILogger<DataAggregationService> logger)
    {
        _candleRepository = candleRepository;
        _tickRepository = tickRepository;
        _instrumentRepository = instrumentRepository;
        _options = options.Value;
        _logger = logger;

        _flushTimer = new Timer(
            _ => _ = FlushAsync(CancellationToken.None),
            state: null,
            dueTime: TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
            period: TimeSpan.FromSeconds(_options.FlushIntervalSeconds));
    }

    /// <inheritdoc />
    public async Task HandleAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        // Используем in-memory кэш для инструментов (избегаем 300+ запросов/сек к БД)
        var instrumentKey = new InstrumentKey(tick.Symbol, tick.Exchange);

        if (!_instrumentCache.TryGetValue(instrumentKey, out var instrument))
        {
            instrument = await _instrumentRepository.GetOrCreateAsync(
                tick.Symbol, tick.Exchange, cancellationToken);
            _instrumentCache.TryAdd(instrumentKey, instrument);
        }

        _tickBuffer.Enqueue(new Tick
        {
            InstrumentId = instrument.Id,
            Exchange = tick.Exchange,
            Symbol = tick.Symbol,
            Price = tick.Price,
            Volume = tick.Volume,
            Timestamp = tick.Timestamp,
            ReceivedAt = tick.ReceivedAt,
            SourceType = tick.SourceType
        });

        foreach (var interval in _options.CandleIntervals)
        {
            var openTime = TruncateToInterval(tick.Timestamp, interval);
            var key = new CandleKey(instrument.Id, interval, openTime);

            var candle = _candles.GetOrAdd(key, _ => new Candle
            {
                InstrumentId = instrument.Id,
                Interval = interval,
                OpenTime = openTime,
                CloseTime = openTime + interval.ToTimeSpan()
            });

            candle.ApplyTick(tick.Price, tick.Volume);
        }

        if (_tickBuffer.Count >= _options.TickBufferSize)
        {
            await FlushTickBufferAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Flushes completed candles and buffered ticks to storage.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0)
            return;

        try
        {
            await FlushTickBufferAsync(cancellationToken);
            await FlushCandlesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic flush");
        }
        finally
        {
            Interlocked.Exchange(ref _isFlushing, 0);
        }
    }

    private async Task FlushTickBufferAsync(CancellationToken cancellationToken)
    {
        var ticks = DrainTickBuffer();
        if (ticks.Count == 0)
            return;

        _logger.LogDebug("Flushing {Count} ticks to storage", ticks.Count);

        await _tickRepository.BulkInsertAsync(ticks, cancellationToken);
    }

    private List<Tick> DrainTickBuffer()
    {
        var ticks = new List<Tick>(_options.TickBufferSize);

        while (ticks.Count < _options.TickBufferSize * 2 && _tickBuffer.TryDequeue(out var tick))
        {
            ticks.Add(tick);
        }

        return ticks;
    }

    private async Task FlushCandlesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var completedCandles = new List<Candle>();
        var staleKeys = new List<CandleKey>();

        foreach (var (key, candle) in _candles)
        {
            if (candle.CloseTime <= now)
            {
                completedCandles.Add(candle);
                staleKeys.Add(key);
            }

            var age = now - candle.OpenTime;
            if (age.TotalMinutes > _options.InMemoryCandleRetentionMinutes && !staleKeys.Contains(key))
            {
                completedCandles.Add(candle);
                staleKeys.Add(key);
            }
        }

        if (completedCandles.Count > 0)
        {
            _logger.LogDebug("Flushing {Count} completed candles to storage", completedCandles.Count);

            await _candleRepository.BulkUpsertAsync(completedCandles, cancellationToken);

            foreach (var key in staleKeys)
            {
                _candles.TryRemove(key, out _);
            }
        }
    }

    private static DateTime TruncateToInterval(DateTime timestamp, TimeInterval interval)
    {
        var span = interval.ToTimeSpan();
        var ticks = timestamp.Ticks / span.Ticks * span.Ticks;
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    public async ValueTask DisposeAsync()
    {
        if (_flushTimer is not null)
        {
            await _flushTimer.DisposeAsync();
            _flushTimer = null;
        }

        try
        {
            await FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during final flush on dispose");
        }
    }
}
