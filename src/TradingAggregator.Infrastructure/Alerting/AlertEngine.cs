using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting;

public sealed class AlertEngine
{
    private readonly IEnumerable<IAlertRuleEvaluator> _evaluators;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AlertEngine> _logger;

    private readonly ConcurrentDictionary<int, DateTime> _lastTriggered = new();

    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromSeconds(60);

    public AlertEngine(
        IEnumerable<IAlertRuleEvaluator> evaluators,
        IEnumerable<INotificationChannel> channels,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AlertEngine> logger)
    {
        _evaluators = evaluators;
        _channels = channels;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task EvaluateAsync(AlertRule rule, NormalizedTick tick, CancellationToken ct)
    {
        if (!rule.IsActive)
        {
            _logger.LogDebug("Rule {RuleId} '{RuleName}' is inactive, skipping", rule.Id, rule.Name);
            return;
        }

        if (_lastTriggered.TryGetValue(rule.Id, out var lastTime)
            && DateTime.UtcNow - lastTime < CooldownPeriod)
        {
            _logger.LogDebug(
                "Rule {RuleId} '{RuleName}' is in cooldown until {CooldownEnd}",
                rule.Id, rule.Name, lastTime + CooldownPeriod);
            return;
        }

        var evaluator = _evaluators.FirstOrDefault(e => e.CanEvaluate(rule));
        if (evaluator is null)
        {
            _logger.LogWarning("No evaluator found for rule type {RuleType}", rule.RuleType);
            return;
        }

        try
        {
            var result = await evaluator.EvaluateAsync(rule, tick, ct);

            if (!result.IsTriggered)
                return;

            _logger.LogInformation(
                "Rule {RuleId} '{RuleName}' triggered: {Message}",
                rule.Id, rule.Name, result.Message);

            _lastTriggered[rule.Id] = DateTime.UtcNow;

            var channelTasks = _channels.Select(async channel =>
            {
                try
                {
                    await channel.SendAsync(result.Message, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send notification via channel {ChannelName}",
                        channel.Name);
                }
            });

            await Task.WhenAll(channelTasks);

            using var scope = _serviceScopeFactory.CreateScope();
            var alertHistoryRepository = scope.ServiceProvider.GetRequiredService<IAlertHistoryRepository>();

            var history = new AlertHistory
            {
                RuleId = rule.Id,
                InstrumentId = rule.InstrumentId,
                Message = result.Message,
                TriggeredAt = DateTime.UtcNow
            };

            await alertHistoryRepository.AddAsync(history, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error evaluating rule {RuleId} '{RuleName}'",
                rule.Id, rule.Name);
        }
    }
}
