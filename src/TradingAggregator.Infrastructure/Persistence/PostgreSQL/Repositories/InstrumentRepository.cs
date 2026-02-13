using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Persistence.PostgreSQL.Repositories;

public class InstrumentRepository : IInstrumentRepository
{
    private readonly TradingDbContext _context;
    private readonly ILogger<InstrumentRepository> _logger;

    private static readonly string[] KnownQuoteCurrencies =
        ["USDT", "USDC", "BUSD", "USD", "EUR", "BTC", "ETH", "BNB"];

    public InstrumentRepository(TradingDbContext context, ILogger<InstrumentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Instrument?> GetBySymbolAndExchangeAsync(
        string symbol,
        ExchangeType exchange,
        CancellationToken cancellationToken = default)
    {
        return await _context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Symbol == symbol && i.Exchange == exchange, cancellationToken);
    }

    public async Task<Instrument> GetOrCreateAsync(
        string symbol,
        ExchangeType exchange,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Instruments
            .FirstOrDefaultAsync(i => i.Symbol == symbol && i.Exchange == exchange, cancellationToken);

        if (existing is not null)
            return existing;

        var (baseCurrency, quoteCurrency) = SplitSymbol(symbol);

        var instrument = new Instrument
        {
            Symbol = symbol,
            Exchange = exchange,
            BaseCurrency = baseCurrency,
            QuoteCurrency = quoteCurrency,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created instrument {Symbol} on {Exchange} (Base={Base}, Quote={Quote})",
            symbol, exchange, baseCurrency, quoteCurrency);

        return instrument;
    }

    public async Task<IReadOnlyList<Instrument>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Instruments
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<Instrument?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    private static (string BaseCurrency, string QuoteCurrency) SplitSymbol(string symbol)
    {
        var upperSymbol = symbol.ToUpperInvariant();

        foreach (var quote in KnownQuoteCurrencies)
        {
            if (upperSymbol.EndsWith(quote, StringComparison.Ordinal) && upperSymbol.Length > quote.Length)
            {
                var baseCurrency = upperSymbol[..^quote.Length];
                return (baseCurrency, quote);
            }
        }

        if (upperSymbol.Length >= 6)
        {
            var mid = upperSymbol.Length / 2;
            return (upperSymbol[..mid], upperSymbol[mid..]);
        }

        return (upperSymbol, string.Empty);
    }
}
