using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingAggregator.Application.Options;
using TradingAggregator.Application.Pipeline;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Application.Services;

/// <summary>
/// Validates each incoming tick against active alert rules
/// and sends notifications when rules are triggered.
/// </summary>
public sealed class AlertService : ITickPipelineHandler
{
    private readonly IAlertRuleRepository _alertRuleRepository;
    private readonly IAlertHistoryRepository _alertHistoryRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IEnumerable<IAlertRuleEvaluator> _evaluators;
    private readonly IEnumerable<INotificationChannel> _notificationChannels;
    private readonly AlertOptions _options;
    private readonly ILogger<AlertService> _logger;

    /// <summary>
    /// Tracks the last trigger time for each rule to control cooldown period.
    /// Key = AlertRule.Id, Value = last trigger time (UTC).
    /// </summary>
    private readonly Dictionary<int, DateTime> _lastTriggered = new();

    public AlertService(
        IAlertRuleRepository alertRuleRepository,
        IAlertHistoryRepository alertHistoryRepository,
        IInstrumentRepository instrumentRepository,
        IEnumerable<IAlertRuleEvaluator> evaluators,
        IEnumerable<INotificationChannel> notificationChannels,
        IOptions<AlertOptions> options,
        ILogger<AlertService> logger)
    {
        _alertRuleRepository = alertRuleRepository;
        _alertHistoryRepository = alertHistoryRepository;
        _instrumentRepository = instrumentRepository;
        _evaluators = evaluators;
        _notificationChannels = notificationChannels;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(NormalizedTick tick, CancellationToken cancellationToken)
    {
        var activeRules = await _alertRuleRepository.GetAllActiveAsync(cancellationToken);

        if (activeRules.Count == 0)
            return;

        var instrument = await _instrumentRepository.GetBySymbolAndExchangeAsync(
            tick.Symbol, tick.Exchange, cancellationToken);

        if (instrument is null)
            return;

        var relevantRules = activeRules
            .Where(r => r.InstrumentId == instrument.Id)
            .ToList();

        foreach (var rule in relevantRules)
        {
            await EvaluateRuleAsync(rule, tick, cancellationToken);
        }
    }

    private async Task EvaluateRuleAsync(
        AlertRule rule,
        NormalizedTick tick,
        CancellationToken cancellationToken)
    {
        var evaluator = _evaluators.FirstOrDefault(e => e.CanEvaluate(rule));

        if (evaluator is null)
        {
            _logger.LogWarning(
                "No evaluator found for alert rule {RuleId} of type {RuleType}",
                rule.Id,
                rule.RuleType);
            return;
        }

        try
        {
            var result = await evaluator.EvaluateAsync(rule, tick, cancellationToken);

            if (!result.IsTriggered)
                return;

            if (IsInCooldown(rule.Id))
            {
                _logger.LogDebug(
                    "Alert rule {RuleId} triggered but in cooldown period",
                    rule.Id);
                return;
            }

            _lastTriggered[rule.Id] = DateTime.UtcNow;

            _logger.LogInformation(
                "Alert rule {RuleId} ({RuleName}) triggered: {Message}",
                rule.Id,
                rule.Name,
                result.Message);

            await _alertHistoryRepository.AddAsync(new AlertHistory
            {
                RuleId = rule.Id,
                InstrumentId = rule.InstrumentId,
                Message = result.Message,
                TriggeredAt = DateTime.UtcNow
            }, cancellationToken);

            await SendNotificationsAsync(result.Message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error evaluating alert rule {RuleId} for {Symbol}",
                rule.Id,
                tick.Symbol);
        }
    }

    private bool IsInCooldown(int ruleId)
    {
        if (!_lastTriggered.TryGetValue(ruleId, out var lastTime))
            return false;

        return (DateTime.UtcNow - lastTime).TotalSeconds < _options.CooldownSeconds;
    }

    private async Task SendNotificationsAsync(string message, CancellationToken cancellationToken)
    {
        var tasks = _notificationChannels
            .Select(async channel =>
            {
                try
                {
                    await channel.SendAsync(message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send notification via channel {Channel}",
                        channel.Name);
                }
            });

        await Task.WhenAll(tasks);
    }
}
