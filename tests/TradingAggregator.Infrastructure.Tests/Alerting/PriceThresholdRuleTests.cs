using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Infrastructure.Alerting.Rules;

namespace TradingAggregator.Infrastructure.Tests.Alerting;

public class PriceThresholdRuleTests
{
    private readonly Mock<ILogger<PriceThresholdRule>> _loggerMock;
    private readonly PriceThresholdRule _rule;

    public PriceThresholdRuleTests()
    {
        _loggerMock = new Mock<ILogger<PriceThresholdRule>>();
        _rule = new PriceThresholdRule(_loggerMock.Object);
    }

    [Fact]
    public void CanEvaluate_PriceAboveRule_ReturnsTrue()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceAbove,
            Threshold = 50000m
        };

        // Act
        var result = _rule.CanEvaluate(alertRule);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEvaluate_PriceBelowRule_ReturnsTrue()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceBelow,
            Threshold = 50000m
        };

        // Act
        var result = _rule.CanEvaluate(alertRule);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEvaluate_OtherRuleType_ReturnsFalse()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m
        };

        // Act
        var result = _rule.CanEvaluate(alertRule);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_PriceAbove_WhenPriceExceedsThreshold_ReturnsTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceAbove,
            Threshold = 50000m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50001m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsTriggered.Should().BeTrue();
        result.Message.Should().Contain("BTCUSDT");
        result.Message.Should().Contain("50001");
        result.Message.Should().Contain("50000");
    }

    [Fact]
    public async Task EvaluateAsync_PriceAbove_WhenPriceBelowThreshold_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceAbove,
            Threshold = 50000m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 49999m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_PriceAbove_WhenPriceEqualsThreshold_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceAbove,
            Threshold = 50000m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_PriceBelow_WhenPriceBelowThreshold_ReturnsTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceBelow,
            Threshold = 50000m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 49999m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeTrue();
        result.Message.Should().Contain("BTCUSDT");
        result.Message.Should().Contain("49999");
        result.Message.Should().Contain("50000");
    }

    [Fact]
    public async Task EvaluateAsync_PriceBelow_WhenPriceAboveThreshold_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceBelow,
            Threshold = 50000m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50001m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_PriceBelow_WhenPriceEqualsThreshold_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceBelow,
            Threshold = 50000m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50000m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_UnsupportedRuleType_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 5m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 100000m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceAbove,
            Threshold = 50000m
        };

        var tick = new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = "BTCUSDT",
            Price = 50001m,
            Volume = 1m,
            Timestamp = DateTime.UtcNow
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _rule.EvaluateAsync(alertRule, tick, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
