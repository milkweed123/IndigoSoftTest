using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Domain.Interfaces;

public interface IAlertRuleRepository
{
    Task<AlertRule> CreateAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task<AlertRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IAlertHistoryRepository
{
    Task AddAsync(AlertHistory history, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertHistory>> GetAsync(
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
