using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Repositories;

public class AlertRuleRepository : IAlertRuleRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<AlertRuleRepository> _logger;

    public AlertRuleRepository(TradingDbContext context, ILogger<AlertRuleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AlertRule> CreateAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        _context.AlertRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created alert rule {RuleId}: {Name}", rule.Id, rule.Name);

        return rule;
    }

    public async Task<AlertRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.AlertRules
            .AsNoTracking()
            .Include(r => r.Instrument)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AlertRule>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AlertRules
            .AsNoTracking()
            .Include(r => r.Instrument)
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        _context.AlertRules.Update(rule);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated alert rule {RuleId}", rule.Id);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var rule = await _context.AlertRules.FindAsync([id], cancellationToken);

        if (rule is not null)
        {
            _context.AlertRules.Remove(rule);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted alert rule {RuleId}", id);
        }
    }
}
