using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using TradingAggregator.Infrastructure.Alerting.Rules;

namespace TradingAggregator.Infrastructure.Tests.Alerting;

public class VolumeChangeRuleTests
{
    private readonly Mock<ILogger<VolumeChangeRule>> _loggerMock;
    private readonly VolumeChangeRule _rule;

    public VolumeChangeRuleTests()
    {
        _loggerMock = new Mock<ILogger<VolumeChangeRule>>();
        _rule = new VolumeChangeRule(_loggerMock.Object);
    }

    [Fact]
    public void CanEvaluate_VolumeSpikeRule_ReturnsTrue()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m
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
    public async Task EvaluateAsync_LessThanTwoTicks_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = 5
        };

        var tick = CreateTick("BTCUSDT", 100m, 1.5m, DateTime.UtcNow);

        // Act
        var result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_VolumeSpike_ReturnsTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m, // 3x average
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, 1m, baseTime),
            CreateTick("BTCUSDT", 101m, 1m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, 1m, baseTime.AddMinutes(2)),
            CreateTick("BTCUSDT", 103m, 5m, baseTime.AddMinutes(3)) // Spike: 5x vs avg of 1
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
        result.Message.Should().Contain("Volume spike");
        result.Message.Should().Contain("5");
    }

    [Fact]
    public async Task EvaluateAsync_VolumeNormalIncrease_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, 1m, baseTime),
            CreateTick("BTCUSDT", 101m, 1.2m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, 1.5m, baseTime.AddMinutes(2)),
            CreateTick("BTCUSDT", 103m, 2m, baseTime.AddMinutes(3)) // Only 2x vs avg of ~1.1
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
    public async Task EvaluateAsync_ZeroAverageVolume_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, 0m, baseTime),
            CreateTick("BTCUSDT", 101m, 0m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, 5m, baseTime.AddMinutes(2))
        };

        // Act
        AlertEvaluationResult? result = null;
        foreach (var tick in ticks)
        {
            result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        // Assert
        result.Should().NotBeNull();
        result!.IsTriggered.Should().BeFalse(); // Zero average protection
    }

    [Fact]
    public async Task EvaluateAsync_OldTicksRemoved_AfterPeriodExpires()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var oldTick = CreateTick("BTCUSDT", 100m, 10m, baseTime); // Old high volume
        var recentTick = CreateTick("BTCUSDT", 101m, 2m, baseTime.AddMinutes(6)); // Recent, outside period

        // Act
        await _rule.EvaluateAsync(alertRule, oldTick, CancellationToken.None);
        var result = await _rule.EvaluateAsync(alertRule, recentTick, CancellationToken.None);

        // Assert
        result.IsTriggered.Should().BeFalse(); // Less than 2 ticks in window
    }

    [Fact]
    public async Task EvaluateAsync_DifferentSymbols_TrackedSeparately()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;

        // BTC has volume spike
        var btcTicks = new[]
        {
            CreateTick("BTCUSDT", 100m, 1m, baseTime),
            CreateTick("BTCUSDT", 101m, 1m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, 5m, baseTime.AddMinutes(2)) // Spike
        };

        // ETH has normal volume
        var ethTicks = new[]
        {
            CreateTick("ETHUSDT", 100m, 1m, baseTime),
            CreateTick("ETHUSDT", 101m, 1m, baseTime.AddMinutes(1)),
            CreateTick("ETHUSDT", 102m, 1.5m, baseTime.AddMinutes(2)) // Normal
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
    public async Task EvaluateAsync_ExactlyAtThreshold_ReturnsNotTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, 1m, baseTime),
            CreateTick("BTCUSDT", 101m, 1m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, 3m, baseTime.AddMinutes(2)) // Exactly 3x
        };

        // Act
        AlertEvaluationResult? result = null;
        foreach (var tick in ticks)
        {
            result = await _rule.EvaluateAsync(alertRule, tick, CancellationToken.None);
        }

        // Assert
        result.Should().NotBeNull();
        result!.IsTriggered.Should().BeFalse(); // Must be > threshold, not >=
    }

    [Fact]
    public async Task EvaluateAsync_JustAboveThreshold_ReturnsTriggered()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = 5
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, 1m, baseTime),
            CreateTick("BTCUSDT", 101m, 1m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, 3.01m, baseTime.AddMinutes(2)) // Just above 3x
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
    public async Task EvaluateAsync_DefaultPeriod_UsesDefaultWhenNotSpecified()
    {
        // Arrange
        var alertRule = new AlertRule
        {
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m,
            PeriodMinutes = null // Will use default (5 minutes)
        };

        var baseTime = DateTime.UtcNow;
        var ticks = new[]
        {
            CreateTick("BTCUSDT", 100m, 1m, baseTime),
            CreateTick("BTCUSDT", 101m, 1m, baseTime.AddMinutes(1)),
            CreateTick("BTCUSDT", 102m, 5m, baseTime.AddMinutes(2))
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
            RuleType = AlertRuleType.VolumeSpike,
            Threshold = 3m
        };

        var tick = CreateTick("BTCUSDT", 100m, 1m, DateTime.UtcNow);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _rule.EvaluateAsync(alertRule, tick, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static NormalizedTick CreateTick(string symbol, decimal price, decimal volume, DateTime timestamp)
    {
        return new NormalizedTick
        {
            Exchange = ExchangeType.Binance,
            Symbol = symbol,
            Price = price,
            Volume = volume,
            Timestamp = timestamp,
            SourceType = DataSourceType.WebSocket
        };
    }
}
