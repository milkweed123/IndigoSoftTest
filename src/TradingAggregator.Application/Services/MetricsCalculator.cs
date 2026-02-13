using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Application.Services;

/// <summary>
/// Calculates derived market metrics: average price for a period
/// and volatility (standard deviation of log returns).
/// </summary>
public static class MetricsCalculator
{
    /// <summary>
    /// Calculates simple average price from a collection of ticks.
    /// </summary>
    /// <param name="ticks">Tick data, ordered by time.</param>
    /// <returns>Average price or zero if no ticks.</returns>
    public static decimal AveragePrice(IReadOnlyList<Tick> ticks)
    {
        if (ticks.Count == 0)
            return 0m;

        var sum = 0m;
        foreach (var tick in ticks)
        {
            sum += tick.Price;
        }

        return sum / ticks.Count;
    }

    /// <summary>
    /// Calculates volume-weighted average price (VWAP) from a collection of ticks.
    /// </summary>
    public static decimal VolumeWeightedAveragePrice(IReadOnlyList<Tick> ticks)
    {
        if (ticks.Count == 0)
            return 0m;

        var totalVolume = 0m;
        var totalPriceVolume = 0m;

        foreach (var tick in ticks)
        {
            totalPriceVolume += tick.Price * tick.Volume;
            totalVolume += tick.Volume;
        }

        return totalVolume == 0m ? 0m : totalPriceVolume / totalVolume;
    }

    /// <summary>
    /// Calculates volatility as standard deviation
    /// of log returns between consecutive ticks.
    /// </summary>
    /// <param name="ticks">Tick data, ordered by time. Minimum 2 ticks required.</param>
    /// <returns>Standard deviation of log returns or zero if insufficient data.</returns>
    public static double Volatility(IReadOnlyList<Tick> ticks)
    {
        if (ticks.Count < 2)
            return 0.0;

        var returns = new List<double>(ticks.Count - 1);

        for (var i = 1; i < ticks.Count; i++)
        {
            var previousPrice = ticks[i - 1].Price;
            var currentPrice = ticks[i].Price;

            if (previousPrice <= 0)
                continue;

            var logReturn = Math.Log((double)(currentPrice / previousPrice));
            returns.Add(logReturn);
        }

        if (returns.Count == 0)
            return 0.0;

        return StandardDeviation(returns);
    }

    /// <summary>
    /// Calculates volatility from a series of candle close prices.
    /// </summary>
    public static double VolatilityFromCandles(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 2)
            return 0.0;

        var returns = new List<double>(candles.Count - 1);

        for (var i = 1; i < candles.Count; i++)
        {
            var previousClose = candles[i - 1].ClosePrice;
            var currentClose = candles[i].ClosePrice;

            if (previousClose <= 0)
                continue;

            var logReturn = Math.Log((double)(currentClose / previousClose));
            returns.Add(logReturn);
        }

        if (returns.Count == 0)
            return 0.0;

        return StandardDeviation(returns);
    }

    private static double StandardDeviation(List<double> values)
    {
        if (values.Count == 0)
            return 0.0;

        var mean = 0.0;
        foreach (var v in values)
        {
            mean += v;
        }
        mean /= values.Count;

        var sumSquaredDiffs = 0.0;
        foreach (var v in values)
        {
            var diff = v - mean;
            sumSquaredDiffs += diff * diff;
        }

        return Math.Sqrt(sumSquaredDiffs / values.Count);
    }
}
