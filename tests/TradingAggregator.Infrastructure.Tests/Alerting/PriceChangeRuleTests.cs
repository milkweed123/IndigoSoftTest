using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using TradingAggregator.Infrastructure.Alerting.Rules;

namespace TradingAggregator.Infrastructure.Tests.Alerting;

public class PriceChangeRuleTests
{
    private readonly Mock<ILogger<PriceChangeRule>> _loggerMock;
    private readonly PriceChangeRule _rule;

    public PriceChangeRuleTests()
    {
        _loggerMock = new Mock<ILogger<PriceChangeRule>>();
        _rule = new PriceChangeRule(_loggerMock.Object);
    }

    [Fact]
    public void CanEvaluate_PriceChangePercentRule_ReturnsTrue()
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
        result.Should().BeTrue();
    }

    [Fact]
    public void CanEvaluate_OtherRuleType_ReturnsFalse()
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
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_FirstTick_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var tick = CreateTick("BTCUSDT", 50000m, DateTime.UtcNow);

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_PriceChangeExceedsThreshold_ReturnsTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var tick1 = CreateTick("BTCUSDT", 100m, baseTime);
        var tick2 = CreateTick("BTCUSDT", 106m, baseTime.AddMinutes(2)); // 6% increase

        // Act
        await _rule.EvaluateAsync(alertRule, tick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, tick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeTrue();
        result.Message.Should().Contain("BTCUSDT");
        result.Message.Should().Contain("%");
    }

    [Fact]
    public async Task EvaluateAsync_PriceChangeBelowThreshold_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var tick1 = CreateTick("BTCUSDT", 100m, baseTime);
        var tick2 = CreateTick("BTCUSDT", 103m, baseTime.AddMinutes(2)); // 3% increase

        // Act
        await _rule.EvaluateAsync(alertRule, tick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, tick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_NegativePriceChangeExceedsThreshold_ReturnsTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var tick1 = CreateTick("BTCUSDT", 100m, baseTime);
        var tick2 = CreateTick("BTCUSDT", 94m, baseTime.AddMinutes(2)); // -6% decrease

        // Act
        await _rule.EvaluateAsync(alertRule, tick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, tick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_PeriodExpires_ResetsBaseline()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var tick1 = CreateTick("BTCUSDT", 100m, baseTime);
        var tick2 = CreateTick("BTCUSDT", 110m, baseTime.AddMinutes(6)); // After period expires

        // Act
        await _rule.EvaluateAsync(alertRule, tick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, tick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse(); // New baseline, not triggered
    }

    [Fact]
    public async Task EvaluateAsync_ZeroFirstPrice_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var tick1 = CreateTick("BTCUSDT", 0m, baseTime);
        var tick2 = CreateTick("BTCUSDT", 100m, baseTime.AddMinutes(2));

        // Act
        await _rule.EvaluateAsync(alertRule, tick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, tick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse(); // Division by zero protection
    }

    [Fact]
    public async Task EvaluateAsync_DefaultPeriod_UsesDefaultWhenNotSpecified()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = null // Will use default (5 minutes)
        };

        var baseTime = DateTime.UtcNow;
        var tick1 = CreateTick("BTCUSDT", 100m, baseTime);
        var tick2 = CreateTick("BTCUSDT", 106m, baseTime.AddMinutes(3));

        // Act
        await _rule.EvaluateAsync(alertRule, tick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, tick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_DifferentSymbols_TrackedSeparately()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var btcTick1 = CreateTick("BTCUSDT", 100m, baseTime);
        var ethTick1 = CreateTick("ETHUSDT", 200m, baseTime);
        var btcTick2 = CreateTick("BTCUSDT", 106m, baseTime.AddMinutes(2));
        var ethTick2 = CreateTick("ETHUSDT", 203m, baseTime.AddMinutes(2)); // Only 1.5% change

        // Act
        await _rule.EvaluateAsync(alertRule, btcTick1, CancellationToken.None);
        await _rule.EvaluateAsync(alertRule, ethTick1, CancellationToken.None);
        var btcResult = await _rule.EvaluateAsync(alertRule, btcTick2, CancellationToken.None);
        var ethResult = await _rule.EvaluateAsync(alertRule, ethTick2, CancellationToken.None);

        // Assert
        btcResult.IsTriggered.Should().BeTrue();
        ethResult.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.PriceChangePercent,
            Threshold = 5m
        };

        var tick = CreateTick("BTCUSDT", 100m, DateTime.UtcNow);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _rule.EvaluateAsync(alertRule, tick, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static NormalizedTick CreateTick(string symbol, decimal price, DateTime timestamp)
    {
        return new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = symbol,
            Price = price,
            Volume = 1m,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };
    }
}
