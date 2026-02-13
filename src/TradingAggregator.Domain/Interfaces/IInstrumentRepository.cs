using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Domain.Interfaces;

public interface IInstrumentRepository
{
    Task<Instrument?> GetBySymbolAndExchangeAsync(string symbol, ExchangeType exchange, CancellationToken cancellationToken = default);
    Task<Instrument> GetOrCreateAsync(string symbol, ExchangeType exchange, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Instrument>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Instrument?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
