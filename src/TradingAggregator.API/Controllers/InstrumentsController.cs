using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradingAggregator.Application.Mappings;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.API.Controllers;

/// <summary>
/// Provides access to instrument metadata (trading pairs and exchanges).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InstrumentsController : ControllerBase
{
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly ILogger<InstrumentsController> _logger;
    private readonly IMapper _mapper;

    public InstrumentsController(
        IInstrumentRepository instrumentRepository,
        ILogger<InstrumentsController> logger,
        IMapper mapper)
    {
        _instrumentRepository = instrumentRepository;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all active instruments (trading pairs) available in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of instrument metadata</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InstrumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<InstrumentDto>>> GetAllInstruments(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instruments = await _instrumentRepository.GetAllActiveAsync(cancellationToken);

            var dtos = _mapper.Map<List<InstrumentDto>>(instruments);

            return Ok(dtos);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetAllInstruments");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving instruments");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving instruments" });
        }
    }

    /// <summary>
    /// Gets a specific instrument by its identifier.
    /// </summary>
    /// <param name="id">Instrument identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Instrument metadata</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(InstrumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<InstrumentDto>> GetInstrumentById(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var instrument = await _instrumentRepository.GetByIdAsync(id, cancellationToken);

            if (instrument is null)
            {
                return NotFound(new { error = $"Instrument with ID {id} not found" });
            }

            var dto = _mapper.Map<InstrumentDto>(instrument);

            return Ok(dto);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetInstrumentById with ID {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving instrument with ID {Id}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving the instrument" });
        }
    }
}
