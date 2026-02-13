using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradingAggregator.Application.DTOs;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.API.Controllers;

/// <summary>
/// Provides access to raw tick data with filtering and pagination support.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TicksController : ControllerBase
{
    private readonly ITickRepository _tickRepository;
    private readonly ILogger<TicksController> _logger;
    private readonly IMapper _mapper;

    public TicksController(
        ITickRepository tickRepository,
        ILogger<TicksController> logger,
        IMapper mapper)
    {
        _tickRepository = tickRepository;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets tick data with optional filtering by symbol, exchange, and time range.
    /// </summary>
    /// <param name="symbol">Trading pair symbol (e.g., BTCUSDT)</param>
    /// <param name="exchange">Exchange name (optional, dropdown)</param>
    /// <param name="from">Start timestamp (UTC, optional)</param>
    /// <param name="to">End timestamp (UTC, optional)</param>
    /// <param name="limit">Maximum number of results (default: 100, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tick data transfer objects</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TickDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<TickDto>>> GetTicks(
        [FromQuery] string symbol,
        [FromQuery] ExchangeType? exchange = null,
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
            var ticks = await _tickRepository.GetAsync(
                symbol: symbol.ToUpperInvariant(),
                exchange: exchange,
                from: from,
                to: to,
                limit: limit,
                cancellationToken: cancellationToken);

            var dtos = _mapper.Map<List<TickDto>>(ticks);

            return Ok(dtos);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetTicks with symbol {Symbol}", symbol);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ticks for symbol {Symbol}", symbol);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving tick data" });
        }
    }
}
