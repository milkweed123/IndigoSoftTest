using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Application.Pipeline;

/// <summary>
/// Determines if a normalized tick is unique and has not been received before.
/// Implementations may use in-memory sets, Redis, or similar stores.
/// </summary>
public interface IDeduplicator
{
    /// <summary>
    /// Returns <c>true</c> if the tick has not been received before; <c>false</c> if it's a duplicate.
    /// </summary>
    Task<bool> IsUniqueAsync(NormalizedTick tick, CancellationToken cancellationToken);
}
