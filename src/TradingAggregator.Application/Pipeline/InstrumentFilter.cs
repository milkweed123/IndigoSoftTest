using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingAggregator.Application.Options;
using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Application.Pipeline;

/// <summary>
/// Filters normalized ticks by allowed symbols from <see cref="DataSourceOptions"/>.
/// Ticks for instruments not present in exchange configuration are discarded.
/// </summary>
public sealed class InstrumentFilter
{
    private readonly HashSet<string> _allowedSymbols;
    private readonly ILogger<InstrumentFilter> _logger;

    public InstrumentFilter(
        IOptions<DataSourceOptions> options,
        ILogger<InstrumentFilter> logger)
    {
        _logger = logger;

        _allowedSymbols = options.Value.Exchanges
            .SelectMany(e => e.Symbols)
            .Select(s => s.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "InstrumentFilter initialized with {Count} allowed symbols",
            _allowedSymbols.Count);
    }

    /// <summary>
    /// Returns <c>true</c> if the tick symbol is in the set of allowed symbols.
    /// </summary>
    public bool IsAllowed(NormalizedTick tick)
    {
        var allowed = _allowedSymbols.Contains(tick.Symbol);

        if (!allowed)
        {
            _logger.LogDebug(
                "Tick for {Symbol} on {Exchange} filtered out — not in allowed symbols",
                tick.Symbol,
                tick.Exchange);
        }

        return allowed;
    }
}
