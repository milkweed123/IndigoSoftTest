using Microsoft.AspNetCore.Mvc;
using TradingAggregator.Application.DTOs;
using TradingAggregator.Application.Services;

namespace TradingAggregator.API.Controllers;

/// <summary>
/// Provides system monitoring, health checks, and exchange status information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MonitoringController : ControllerBase
{
    private readonly MonitoringService _monitoringService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        MonitoringService monitoringService,
        ILogger<MonitoringController> logger)
    {
        _monitoringService = monitoringService;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive system statistics, including tick processing metrics,
    /// exchange status, and performance indicators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>System statistics snapshot</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(StatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StatisticsDto>> GetStatistics(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _monitoringService.GetStatisticsAsync(cancellationToken);
            return Ok(statistics);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetStatistics");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving statistics" });
        }
    }

    /// <summary>
    /// Gets the current connection status of all configured exchange adapters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of exchange statuses</returns>
    [HttpGet("exchange-status")]
    [ProducesResponseType(typeof(IEnumerable<ExchangeStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ExchangeStatusDto>>> GetExchangeStatus(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _monitoringService.GetStatisticsAsync(cancellationToken);
            return Ok(statistics.ExchangeStatuses);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetExchangeStatus");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving exchange status");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving exchange status" });
        }
    }

    /// <summary>
    /// Simple health check endpoint that returns service status.
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        var response = new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow
        };
        return Ok(response);
    }
}

/// <summary>
/// Data transfer object for health check responses.
/// </summary>
public class HealthResponse
{
    public string Status { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
