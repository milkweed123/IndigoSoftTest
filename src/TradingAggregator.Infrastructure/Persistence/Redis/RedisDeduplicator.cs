using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TradingAggregator.Application.Pipeline;
using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Infrastructure.Persistence.Redis;

public class RedisDeduplicator : IDeduplicator
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDeduplicator> _logger;

    private static readonly TimeSpan KeyTtl = TimeSpan.FromSeconds(60);

    public RedisDeduplicator(IConnectionMultiplexer redis, ILogger<RedisDeduplicator> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> IsUniqueAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();

        var window = tick.Timestamp.ToString("yyyyMMddHHmm");
        var key = $"dedup:{window}";
        var member = tick.DeduplicationKey;

        var wasAdded = await db.SetAddAsync(key, member);

        if (wasAdded)
        {
            // Устанавливаем TTL только если ключ новый
            await db.KeyExpireAsync(key, KeyTtl);

            _logger.LogTrace("Tick is unique: {DeduplicationKey}", member);
            return true;
        }

        _logger.LogTrace("Duplicate tick detected: {DeduplicationKey}", member);
        return false;
    }
}
