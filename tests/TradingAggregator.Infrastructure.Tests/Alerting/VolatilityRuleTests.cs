using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using TradingAggregator.Infrastructure.Alerting.Rules;

namespace TradingAggregator.Infrastructure.Tests.Alerting;

public class VolatilityRuleTests
{
    private readonly Mock<ILogger<VolatilityRule>> _loggerMock;
    private readonly VolatilityRule _rule;

    public VolatilityRuleTests()
    {
        _loggerMock = new Mock<ILogger<VolatilityRule>>();
        _rule = new VolatilityRule(_loggerMock.Object);
    }

    [Fact]
    public void CanEvaluate_VolatilityRule_ReturnsTrue()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
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
    public async Task EvaluateAsync_LessThanThreeTicks_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var tick1 = CreateTick("BTCUSDT", 100m, baseTime);
        var tick2 = CreateTick("BTCUSDT", 105m, baseTime.AddMinutes(1));

        // Act
        await _rule.EvaluateAsync(alertRule, tick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, tick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_HighVolatility_ReturnsTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, baseTime),
            CreateTick("BTCUSDT", 120m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 90m, baseTime.AddMinutes(2)),
            CreateTick("BTCUSDT", 115m, baseTime.AddMinutes(3))
        };

        // Act
        AlertEvaluationResult? result = null;
        foreach (var tick in ticks)
        {
            result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        // Assert
        result.Should().NotBeNull();
        result!.IsTriggered.Should().BeTrue();
        result.Message.Should().Contain("BTCUSDT");
        result.Message.Should().Contain("volatility");
    }

    [Fact]
    public async Task EvaluateAsync_LowVolatility_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 10m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, baseTime),
            CreateTick("BTCUSDT", 101m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, baseTime.AddMinutes(2)),
            CreateTick("BTCUSDT", 101m, baseTime.AddMinutes(3))
        };

        // Act
        AlertEvaluationResult? result = null;
        foreach (var tick in ticks)
        {
            result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        // Assert
        result.Should().NotBeNull();
        result!.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_OldTicksRemoved_AfterPeriodExpires()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var oldTick = CreateTick("BTCUSDT", 100m, baseTime);
        var recentTick1 = CreateTick("BTCUSDT", 101m, baseTime.AddMinutes(6));
        var recentTick2 = CreateTick("BTCUSDT", 102m, baseTime.AddMinutes(7));

        // Act
        await _rule.EvaluateAsync(alertRule, oldTick, CancellationToken.None);
        await _rule.EvaluateAsync(alertRule, recentTick1, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, recentTick2, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse(); // Less than 3 ticks in current window
    }

    [Fact]
    public async Task EvaluateAsync_DifferentSymbols_TrackedSeparately()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;

        // BTC has high volatility
        var btcTicks = new[]
        {
            CreateTick("BTCUSDT", 100m, baseTime),
            CreateTick("BTCUSDT", 120m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 90m, baseTime.AddMinutes(2)),
            CreateTick("BTCUSDT", 115m, baseTime.AddMinutes(3))
        };

        // ETH has low volatility
        var ethTicks = new[]
        {
            CreateTick("ETHUSDT", 100m, baseTime),
            CreateTick("ETHUSDT", 101m, baseTime.AddMinutes(1)),
            CreateTick("ETHUSDT", 102m, baseTime.AddMinutes(2)),
            CreateTick("ETHUSDT", 101m, baseTime.AddMinutes(3))
        };

        // Act
        AlertEvaluationResult? btcResult = null;
        AlertEvaluationResult? ethResult = null;

        foreach (var tick in btcTicks)
        {
            btcResult = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        foreach (var tick in ethTicks)
        {
            ethResult = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        // Assert
        btcResult.Should().NotBeNull();
        ethResult.Should().NotBeNull();
        btcResult!.IsTriggered.Should().BeTrue();
        ethResult!.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_ZeroPrice_HandledGracefully()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 5m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, baseTime),
            CreateTick("BTCUSDT", 0m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 105m, baseTime.AddMinutes(2)),
            CreateTick("BTCUSDT", 110m, baseTime.AddMinutes(3))
        };

        // Act
        AlertEvaluationResult? result = null;
        foreach (var tick in ticks)
        {
            result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        // Assert - Should not throw, handled gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DefaultPeriod_UsesDefaultWhenNotSpecified()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
            Threshold = 5m,
            PeriodMinutes = null // Will use default (5 minutes)
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, baseTime),
            CreateTick("BTCUSDT", 120m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 90m, baseTime.AddMinutes(2)),
            CreateTick("BTCUSDT", 115m, baseTime.AddMinutes(3))
        };

        // Act
        AlertEvaluationResult? result = null;
        foreach (var tick in ticks)
        {
            result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        // Assert
        result.Should().NotBeNull();
        result!.IsTriggered.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.Volatility,
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
