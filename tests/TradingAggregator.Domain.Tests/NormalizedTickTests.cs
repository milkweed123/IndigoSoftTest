using FluentAssertions;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Tests;

public class NormalizedTickTests
{
    [Fact]
    public void DeduplicationKey_SameProperties_ReturnsSameKey()
    {
        // Arrange
        var timestamp = DateTime.Parse("2024-01-01T12:00:00Z").ToUniversalTime();

        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            ReceivedAt = DateTime.UtcNow,
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            ReceivedAt = DateTime.UtcNow.AddSeconds(5), // Different ReceivedAt
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void DeduplicationKey_DifferentExchange_ReturnsDifferentKey()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Bybit,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void DeduplicationKey_DifferentSymbol_ReturnsDifferentKey()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "ETHUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void DeduplicationKey_DifferentPrice_ReturnsDifferentKey()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50001m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void DeduplicationKey_DifferentVolume_ReturnsDifferentKey()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 2.0m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void DeduplicationKey_DifferentTimestamp_ReturnsDifferentKey()
    {
        // Arrange
        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = DateTime.Parse("2024-01-01T12:00:00Z").ToUniversalTime(),
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = DateTime.Parse("2024-01-01T12:00:01Z").ToUniversalTime(),
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void DeduplicationKey_DifferentReceivedAt_ReturnsSameKey()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            ReceivedAt = DateTime.UtcNow,
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            ReceivedAt = DateTime.UtcNow.AddSeconds(10),
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().Be(key2); // ReceivedAt is not part of deduplication key
    }

    [Fact]
    public void DeduplicationKey_DifferentSourceType_ReturnsSameKey()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        var tick1 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        var tick2 = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.Rest
        };

        // Act
        var key1 = tick1.DeduplicationKey;
        var key2 = tick2.DeduplicationKey;

        // Assert
        key1.Should().Be(key2); // SourceType is not part of deduplication key
    }

    [Fact]
    public void DeduplicationKey_ContainsExpectedFormat()
    {
        // Arrange
        var timestamp = DateTime.Parse("2024-01-01T12:00:00.0000000Z").ToUniversalTime();

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key = tick.DeduplicationKey;

        // Assert
        key.Should().Contain("Binance");
        key.Should().Contain("BTCUSDT");
        key.Should().Contain("50000");
        key.Should().Match(k => k.Contains("1.5") || k.Contains("1,5")); // Locale-independent
        key.Should().Contain("2024-01-01");
    }

    [Fact]
    public void DeduplicationKey_IsNotEmpty()
    {
        // Arrange
        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = DateTime.UtcNow,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var key = tick.DeduplicationKey;

        // Assert
        key.Should().NotBeNullOrWhiteSpace();
    }
}
