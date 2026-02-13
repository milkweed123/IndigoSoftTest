using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Interfaces;

public interface IExchangeStatusRepository
{
    Task UpsertAsync(ExchangeStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExchangeStatus>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ExchangeStatus?> GetAsync(ExchangeType exchange, DataSourceType sourceType, CancellationToken cancellationToken = default);
}
