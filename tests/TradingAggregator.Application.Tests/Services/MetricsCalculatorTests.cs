using FluentAssertions;
using TradingAggregator.Application.Services;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Application.Tests.Services;

public class MetricsCalculatorTests
{
    #region AveragePrice Tests

    [Fact]
    public void AveragePrice_EmptyList_ReturnsZero()
    {
        // Arrange
        var ticks = new List<Tick>();

        // Act
        var result = MetricsCalculator.AveragePrice(ticks);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public void AveragePrice_SingleTick_ReturnsThatPrice()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m)
        };

        // Act
        var result = MetricsCalculator.AveragePrice(ticks);

        // Assert
        result.Should().Be(100m);
    }

    [Fact]
    public void AveragePrice_MultipleTicks_ReturnsCorrectAverage()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m),
            CreateTick(price: 200m),
            CreateTick(price: 300m)
        };

        // Act
        var result = MetricsCalculator.AveragePrice(ticks);

        // Assert
        result.Should().Be(200m); // (100 + 200 + 300) / 3
    }

    [Fact]
    public void AveragePrice_DecimalPrices_ReturnsAccurateAverage()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 50000.50m),
            CreateTick(price: 50001.25m),
            CreateTick(price: 49999.75m)
        };

        // Act
        var result = MetricsCalculator.AveragePrice(ticks);

        // Assert
        result.Should().Be(50000.50m); // (50000.50 + 50001.25 + 49999.75) / 3
    }

    #endregion

    #region VolumeWeightedAveragePrice Tests

    [Fact]
    public void VolumeWeightedAveragePrice_EmptyList_ReturnsZero()
    {
        // Arrange
        var ticks = new List<Tick>();

        // Act
        var result = MetricsCalculator.VolumeWeightedAveragePrice(ticks);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public void VolumeWeightedAveragePrice_ZeroVolume_ReturnsZero()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m, volume: 0m),
            CreateTick(price: 200m, volume: 0m)
        };

        // Act
        var result = MetricsCalculator.VolumeWeightedAveragePrice(ticks);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public void VolumeWeightedAveragePrice_SingleTick_ReturnsThatPrice()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m, volume: 5m)
        };

        // Act
        var result = MetricsCalculator.VolumeWeightedAveragePrice(ticks);

        // Assert
        result.Should().Be(100m);
    }

    [Fact]
    public void VolumeWeightedAveragePrice_MultipleTicks_ReturnsCorrectVWAP()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m, volume: 1m),  // 100 * 1 = 100
            CreateTick(price: 200m, volume: 2m),  // 200 * 2 = 400
            CreateTick(price: 300m, volume: 1m)   // 300 * 1 = 300
        };
        // Total: 800, Total Volume: 4, VWAP: 800 / 4 = 200

        // Act
        var result = MetricsCalculator.VolumeWeightedAveragePrice(ticks);

        // Assert
        result.Should().Be(200m);
    }

    [Fact]
    public void VolumeWeightedAveragePrice_DifferentWeights_WeighsHigherVolumeMore()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m, volume: 1m),   // 100 * 1 = 100
            CreateTick(price: 200m, volume: 10m)   // 200 * 10 = 2000
        };
        // Total: 2100, Total Volume: 11, VWAP: 2100 / 11 ≈ 190.909...

        // Act
        var result = MetricsCalculator.VolumeWeightedAveragePrice(ticks);

        // Assert
        result.Should().BeApproximately(190.909090909090909090909090909m, 0.000001m);
    }

    #endregion

    #region Volatility Tests

    [Fact]
    public void Volatility_EmptyList_ReturnsZero()
    {
        // Arrange
        var ticks = new List<Tick>();

        // Act
        var result = MetricsCalculator.Volatility(ticks);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void Volatility_SingleTick_ReturnsZero()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m)
        };

        // Act
        var result = MetricsCalculator.Volatility(ticks);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void Volatility_TwoTicksSamePrice_ReturnsZero()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m),
            CreateTick(price: 100m)
        };

        // Act
        var result = MetricsCalculator.Volatility(ticks);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void Volatility_TwoTicksDifferentPrices_ReturnsPositiveValue()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m),
            CreateTick(price: 110m),
            CreateTick(price: 105m)
        };

        // Act
        var result = MetricsCalculator.Volatility(ticks);

        // Assert
        result.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void Volatility_MultipleTicksWithVariation_CalculatesCorrectly()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 100m),
            CreateTick(price: 105m),
            CreateTick(price: 103m),
            CreateTick(price: 107m)
        };

        // Act
        var result = MetricsCalculator.Volatility(ticks);

        // Assert
        result.Should().BeGreaterThan(0.0);
        result.Should().BeLessThan(1.0); // Reasonable volatility range
    }

    [Fact]
    public void Volatility_NegativeOrZeroPrice_SkipsThatReturn()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 0m),      // Will be skipped
            CreateTick(price: 100m),
            CreateTick(price: 110m),
            CreateTick(price: 105m)
        };

        // Act
        var result = MetricsCalculator.Volatility(ticks);

        // Assert
        result.Should().BeGreaterThan(0.0); // Should still calculate based on valid prices
    }

    [Fact]
    public void Volatility_AllZeroPrices_ReturnsZero()
    {
        // Arrange
        var ticks = new List<Tick>
        {
            CreateTick(price: 0m),
            CreateTick(price: 0m),
            CreateTick(price: 0m)
        };

        // Act
        var result = MetricsCalculator.Volatility(ticks);

        // Assert
        result.Should().Be(0.0);
    }

    #endregion

    #region VolatilityFromCandles Tests

    [Fact]
    public void VolatilityFromCandles_EmptyList_ReturnsZero()
    {
        // Arrange
        var candles = new List<Candle>();

        // Act
        var result = MetricsCalculator.VolatilityFromCandles(candles);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void VolatilityFromCandles_SingleCandle_ReturnsZero()
    {
        // Arrange
        var candles = new List<Candle>
        {
            CreateCandle(closePrice: 100m)
        };

        // Act
        var result = MetricsCalculator.VolatilityFromCandles(candles);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void VolatilityFromCandles_TwoCandlesSameClose_ReturnsZero()
    {
        // Arrange
        var candles = new List<Candle>
        {
            CreateCandle(closePrice: 100m),
            CreateCandle(closePrice: 100m)
        };

        // Act
        var result = MetricsCalculator.VolatilityFromCandles(candles);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void VolatilityFromCandles_TwoCandlesDifferentClose_ReturnsPositiveValue()
    {
        // Arrange
        var candles = new List<Candle>
        {
            CreateCandle(closePrice: 100m),
            CreateCandle(closePrice: 110m),
            CreateCandle(closePrice: 105m)
        };

        // Act
        var result = MetricsCalculator.VolatilityFromCandles(candles);

        // Assert
        result.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void VolatilityFromCandles_MultipleCandlesWithVariation_CalculatesCorrectly()
    {
        // Arrange
        var candles = new List<Candle>
        {
            CreateCandle(closePrice: 100m),
            CreateCandle(closePrice: 105m),
            CreateCandle(closePrice: 103m),
            CreateCandle(closePrice: 107m)
        };

        // Act
        var result = MetricsCalculator.VolatilityFromCandles(candles);

        // Assert
        result.Should().BeGreaterThan(0.0);
        result.Should().BeLessThan(1.0);
    }

    [Fact]
    public void VolatilityFromCandles_ZeroClosePrice_SkipsThatReturn()
    {
        // Arrange
        var candles = new List<Candle>
        {
            CreateCandle(closePrice: 0m),
            CreateCandle(closePrice: 100m),
            CreateCandle(closePrice: 110m),
            CreateCandle(closePrice: 105m)
        };

        // Act
        var result = MetricsCalculator.VolatilityFromCandles(candles);

        // Assert
        result.Should().BeGreaterThan(0.0);
    }

    #endregion

    #region Helper Methods

    private static Tick CreateTick(decimal price, decimal volume = 1m)
    {
        return new Tick
        {
            Price = price,
            Volume = volume,
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Timestamp = DateTime.UtcNow
        };
    }

    private static Candle CreateCandle(decimal closePrice)
    {
        return new Candle
        {
            OpenPrice = closePrice,
            HighPrice = closePrice,
            LowPrice = closePrice,
            ClosePrice = closePrice,
            Volume = 1m,
            Interval = TimeInterval.OneMinute,
            OpenTime = DateTime.UtcNow
        };
    }

    #endregion
}
