using FluentAssertions;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Tests;

public class CandleTests
{
    [Fact]
    public void ApplyTick_FirstTick_SetsOpenPrice()
    {
        // Arrange
        var candle = new Candle
        {
            InstrumentId = 1,
            Interval = TimeInterval.OneMinute,
            OpenTime = DateTime.UtcNow
        };

        // Act
        candle.ApplyTick(price: 50000m, volume: 0.5m);

        // Assert
        candle.OpenPrice.Should().Be(50000m);
        candle.HighPrice.Should().Be(50000m);
        candle.LowPrice.Should().Be(50000m);
        candle.ClosePrice.Should().Be(50000m);
        candle.Volume.Should().Be(0.5m);
        candle.TradesCount.Should().Be(1);
    }

    [Fact]
    public void ApplyTick_MultipleTicks_UpdatesOHLCV()
    {
        // Arrange
        var candle = new Candle
        {
            InstrumentId = 1,
            Interval = TimeInterval.OneMinute,
            OpenTime = DateTime.UtcNow
        };

        // Act
        candle.ApplyTick(price: 50000m, volume: 0.5m);
        candle.ApplyTick(price: 51000m, volume: 0.3m); // Higher
        candle.ApplyTick(price: 49000m, volume: 0.2m); // Lower
        candle.ApplyTick(price: 50500m, volume: 0.1m); // Close

        // Assert
        candle.OpenPrice.Should().Be(50000m);
        candle.HighPrice.Should().Be(51000m);
        candle.LowPrice.Should().Be(49000m);
        candle.ClosePrice.Should().Be(50500m);
        candle.Volume.Should().Be(1.1m);
        candle.TradesCount.Should().Be(4);
    }
}
