using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradingAggregator.Application.DTOs;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.API.Controllers;

/// <summary>
/// Provides access to aggregated OHLCV candle data with filtering by interval, symbol, and exchange.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CandlesController : ControllerBase
{
    private readonly ICandleRepository _candleRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly ILogger<CandlesController> _logger;
    private readonly IMapper _mapper;

    public CandlesController(
        ICandleRepository candleRepository,
        IInstrumentRepository instrumentRepository,
        ILogger<CandlesController> logger,
        IMapper mapper)
    {
        _candleRepository = candleRepository;
        _instrumentRepository = instrumentRepository;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets candle data (OHLCV) with optional filtering by symbol, exchange, interval, and time range.
    /// </summary>
    /// <param name="symbol">Trading pair symbol (e.g., BTCUSDT)</param>
    /// <param name="exchange">Exchange name (dropdown)</param>
    /// <param name="interval">Time interval - dropdown: OneMinute, FiveMinutes, OneHour</param>
    /// <param name="from">Start timestamp (UTC, optional)</param>
    /// <param name="to">End timestamp (UTC, optional)</param>
    /// <param name="limit">Maximum number of results (default: 100, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of candle data transfer objects</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CandleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<CandleDto>>> GetCandles(
        [FromQuery] string symbol,
        [FromQuery] ExchangeType exchange,
        [FromQuery] TimeInterval interval,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new { error = "Symbol parameter is required" });
        }

        if (limit <= 0 || limit > 1000)
        {
            return BadRequest(new { error = "Limit must be between 1 and 1000" });
        }

        try
        {
            var instrument = await _instrumentRepository.GetBySymbolAndExchangeAsync(
                symbol.ToUpperInvariant(),
                exchange,
                cancellationToken);

            if (instrument is null)
            {
                return NotFound(new { error = $"Instrument not found: {symbol} on {exchange}" });
            }

            var candles = await _candleRepository.GetAsync(
                instrumentId: instrument.Id,
                interval: interval,
                from: from,
                to: to,
                limit: limit,
                cancellationToken: cancellationToken);

            var dtos = _mapper.Map<List<CandleDto>>(candles);
            foreach (var dto in dtos)
            {
                dto.Symbol = instrument.Symbol;
                dto.Exchange = instrument.Exchange.ToString();
                dto.Interval = interval.ToShortString();
            }

            return Ok(dtos);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetCandles with symbol {Symbol}", symbol);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving candles for symbol {Symbol}", symbol);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving candle data" });
        }
    }
}
