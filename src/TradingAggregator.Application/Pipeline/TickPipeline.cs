using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Application.Pipeline;

/// <summary>
/// Channel-based tick processing pipeline.
///
/// Flow: RawTick -> Normalization -> Deduplication -> Filtering -> Handlers
///
/// Uses bounded channel with capacity of 10,000 for backpressure
/// when processing cannot keep up with incoming tick stream.
/// </summary>
public sealed class TickPipeline : ITickPipeline, IAsyncDisposable
{
    private const int ChannelCapacity = 10_000;

    private readonly Channel<RawTick> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly InstrumentFilter _instrumentFilter;
    private readonly IDeduplicator _deduplicator;
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<TickPipeline> _logger;

    private readonly List<Type> _handlerTypes = [];
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    public TickPipeline(
        IServiceProvider serviceProvider,
        InstrumentFilter instrumentFilter,
        IDeduplicator deduplicator,
        IMetricsCollector metricsCollector,
        ILogger<TickPipeline> logger)
    {
        _serviceProvider = serviceProvider;
        _instrumentFilter = instrumentFilter;
        _deduplicator = deduplicator;
        _metricsCollector = metricsCollector;
        _logger = logger;

        _channel = Channel.CreateBounded<RawTick>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public ChannelWriter<RawTick> InputWriter => _channel.Writer;

    /// <inheritdoc />
    public void RegisterHandler<THandler>() where THandler : ITickPipelineHandler
    {
        var handlerType = typeof(THandler);

        if (_handlerTypes.Contains(handlerType))
        {
            _logger.LogWarning("Handler {Handler} is already registered", handlerType.Name);
            return;
        }

        _handlerTypes.Add(handlerType);
        _logger.LogInformation("Registered pipeline handler: {Handler}", handlerType.Name);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_consumerTask is not null)
            throw new InvalidOperationException("Pipeline is already running.");

        _logger.LogInformation(
            "Starting tick pipeline with {HandlerCount} handler(s), channel capacity {Capacity}",
            _handlerTypes.Count,
            ChannelCapacity);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerTask = ConsumeAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping tick pipeline...");

        _channel.Writer.TryComplete();

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_consumerTask is not null)
        {
            try
            {
                await _consumerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _logger.LogInformation("Tick pipeline stopped");
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pipeline consumer loop started");

        try
        {
            await foreach (var rawTick in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    _metricsCollector.RecordPipelineQueueSize(_channel.Reader.Count);

                    var normalized = Normalizer.Normalize(rawTick);

                    if (!await _deduplicator.IsUniqueAsync(normalized, cancellationToken))
                    {
                        _metricsCollector.RecordDuplicateFiltered(normalized.Exchange);
                        continue;
                    }

                    if (!_instrumentFilter.IsAllowed(normalized))
                        continue;

                    await DispatchToHandlersAsync(normalized, cancellationToken);

                    _metricsCollector.RecordTickProcessed(
                        normalized.Exchange,
                        (DateTime.UtcNow - normalized.ReceivedAt).TotalMilliseconds);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing tick for {Symbol} from {Exchange}",
                        rawTick.Symbol, rawTick.Exchange);
                    _metricsCollector.RecordError(rawTick.Exchange, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Pipeline consumer loop cancelled");
        }
        catch (ChannelClosedException)
        {
            _logger.LogInformation("Pipeline channel closed, consumer loop ending");
        }
    }

    private async Task DispatchToHandlersAsync(
        Domain.Entities.NormalizedTick tick,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        foreach (var handlerType in _handlerTypes)
        {
            try
            {
                var handler = (ITickPipelineHandler)scope.ServiceProvider
                    .GetRequiredService(handlerType);

                await handler.HandleAsync(tick, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Handler {Handler} failed processing tick for {Symbol}",
                    handlerType.Name,
                    tick.Symbol);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        if (_consumerTask is not null)
        {
            try
            {
                await _consumerTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
