using FluentAssertions;
using TradingAggregator.Application.Pipeline;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Application.Tests.Pipeline;

public class NormalizerTests
{
    [Fact]
    public void Normalize_ValidRawTick_ReturnsNormalizedTick()
    {
        // Arrange
        var rawTick = new RawTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "btcusdt",
            Price = 50000m,
            Volume = 1.5m,
            Timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified),
            ReceivedAt = new DateTime(2024, 1, 1, 12, 0, 1, DateTimeKind.Unspecified),
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var result = Normalizer.Normalize(rawTick);

        // Assert
        result.Should().NotBeNull();
        result.Exchange.Should().Be(ExchangeType.Binance);
        result.Symbol.Should().Be("BTCUSDT"); // ToUpperInvariant
        result.Price.Should().Be(50000m);
        result.Volume.Should().Be(1.5m);
        result.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        result.ReceivedAt.Kind.Should().Be(DateTimeKind.Utc);
        result.SourceType.Should().Be(DataSourceType.WebSocket);
    }

    [Fact]
    public void Normalize_LowercaseSymbol_ConvertsToUppercase()
    {
        // Arrange
        var rawTick = new RawTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "ethusdt",
            Price = 3000m,
            Volume = 2m,
            Timestamp = DateTime.UtcNow,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var result = Normalizer.Normalize(rawTick);

        // Assert
        result.Symbol.Should().Be("ETHUSDT");
    }

    [Fact]
    public void Normalize_MixedCaseSymbol_ConvertsToUppercase()
    {
        // Arrange
        var rawTick = new RawTick
        {
            Exchange = ExchangeType.Bybit,
            Symbol = "BtC_UsD",
            Price = 50000m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow,
            SourceType = DataSourceType.Rest
        };

        // Act
        var result = Normalizer.Normalize(rawTick);

        // Assert
        result.Symbol.Should().Be("BTC_USD");
    }

    [Fact]
    public void Normalize_UnspecifiedDateTimeKind_ConvertsToUtc()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        var receivedAt = new DateTime(2024, 1, 1, 12, 0, 1, DateTimeKind.Unspecified);

        var rawTick = new RawTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1m,
            Timestamp = timestamp,
            ReceivedAt = receivedAt,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var result = Normalizer.Normalize(rawTick);

        // Assert
        result.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        result.ReceivedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Normalize_NullRawTick_ThrowsArgumentNullException()
    {
        // Arrange
        RawTick? rawTick = null;

        // Act
        var act = () => Normalizer.Normalize(rawTick!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Normalize_PreservesAllExchangeTypes()
    {
        // Arrange & Act & Assert
        foreach (ExchangeType exchange in Enum.GetValues(typeof(ExchangeType)))
        {
            var rawTick = new RawTick
            {
                Exchange = exchange,
                Symbol = "BTCUSDT",
                Price = 50000m,
                Volume = 1m,
                Timestamp = DateTime.UtcNow,
                SourceType = DataSourceType.WebSocket
            };

            var result = Normalizer.Normalize(rawTick);
            result.Exchange.Should().Be(exchange);
        }
    }

    [Fact]
    public void Normalize_PreservesAllSourceTypes()
    {
        // Arrange & Act & Assert
        foreach (DataSourceType sourceType in Enum.GetValues(typeof(DataSourceType)))
        {
            var rawTick = new RawTick
            {
                Exchange = ExchangeType.Binance,
                Symbol = "BTCUSDT",
                Price = 50000m,
                Volume = 1m,
                Timestamp = DateTime.UtcNow,
                SourceType = sourceType
            };

            var result = Normalizer.Normalize(rawTick);
            result.SourceType.Should().Be(sourceType);
        }
    }

    [Fact]
    public void Normalize_ZeroPrice_IsAllowed()
    {
        // Arrange
        var rawTick = new RawTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 0m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var result = Normalizer.Normalize(rawTick);

        // Assert
        result.Price.Should().Be(0m);
    }

    [Fact]
    public void Normalize_ZeroVolume_IsAllowed()
    {
        // Arrange
        var rawTick = new RawTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 0m,
            Timestamp = DateTime.UtcNow,
            SourceType = DataSourceType.WebSocket
        };

        // Act
        var result = Normalizer.Normalize(rawTick);

        // Assert
        result.Volume.Should().Be(0m);
    }
}
