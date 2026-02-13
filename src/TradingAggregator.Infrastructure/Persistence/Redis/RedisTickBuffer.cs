using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Persistence.Redis;

public class RedisTickBuffer : IAsyncDisposable
{
    private readonly ITickRepository _tickRepository;
    private readonly ILogger<RedisTickBuffer> _logger;
    private readonly ConcurrentQueue<Tick> _queue = new();
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _flushTask;
    private readonly int _batchSize;

    public RedisTickBuffer(
        ITickRepository tickRepository,
        ILogger<RedisTickBuffer> logger,
        int batchSize = 500,
        TimeSpan? flushInterval = null)
    {
        _tickRepository = tickRepository;
        _logger = logger;
        _batchSize = batchSize;

        var interval = flushInterval ?? TimeSpan.FromSeconds(5);
        _timer = new PeriodicTimer(interval);
        _flushTask = RunFlushLoopAsync();
    }

    public void Enqueue(Tick tick)
    {
        _queue.Enqueue(tick);

        if (_queue.Count >= _batchSize)
        {
            _ = FlushAsync(CancellationToken.None);
        }
    }

    public int PendingCount => _queue.Count;

    private async Task RunFlushLoopAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                await FlushAsync(_cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in tick buffer flush loop");
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var batch = new List<Tick>(_batchSize);

        while (batch.Count < _batchSize && _queue.TryDequeue(out var tick))
        {
            batch.Add(tick);
        }

        if (batch.Count == 0)
            return;

        try
        {
            await _tickRepository.BulkInsertAsync(batch, cancellationToken);

            _logger.LogDebug("Flushed {Count} ticks to database", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} ticks to database, re-enqueuing", batch.Count);

            foreach (var tick in batch)
            {
                _queue.Enqueue(tick);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _timer.Dispose();

        try
        {
            await _flushTask;
        }
        catch (OperationCanceledException)
        {
        }

        await FlushAsync(CancellationToken.None);

        _cts.Dispose();

        _logger.LogInformation("RedisTickBuffer disposed. Remaining in queue: {Count}", _queue.Count);
    }
}
