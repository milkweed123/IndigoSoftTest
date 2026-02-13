using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Application.Pipeline;

/// <summary>
/// Converts <see cref="RawTick"/> from exchange adapter
/// to <see cref="NormalizedTick"/> used in the application.
/// </summary>
public static class Normalizer
{
    public static NormalizedTick Normalize(RawTick raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        return new NormalizedTick
        {
            Exchange = raw.Exchange,
            Symbol = raw.Symbol.ToUpperInvariant(),
            Price = raw.Price,
            Volume = raw.Volume,
            Timestamp = DateTime.SpecifyKind(raw.Timestamp, DateTimeKind.Utc),
            ReceivedAt = DateTime.SpecifyKind(raw.ReceivedAt, DateTimeKind.Utc),
            SourceType = raw.SourceType
        };
    }
}
