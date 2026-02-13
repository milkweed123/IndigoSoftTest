using Microsoft.AspNetCore.Mvc;
using TradingAggregator.Application.DTOs;
using TradingAggregator.Application.Services;

namespace TradingAggregator.API.Controllers;

/// <summary>
/// Предоставляет мониторинг системы, проверки работоспособности и информацию о статусе бирж.
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
    /// Получает комплексную статистику системы, включая метрики обработки тиков,
    /// статус бирж и показатели производительности.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Снимок статистики системы</returns>
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
    /// Получает текущий статус подключения всех настроенных адаптеров бирж.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список статусов бирж</returns>
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
    /// Простая конечная точка проверки работоспособности, которая возвращает статус службы.
    /// </summary>
    /// <returns>Статус работоспособности</returns>
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
/// Объект передачи данных для ответов проверки работоспособности.
/// </summary>
public class HealthResponse
{
    public string Status { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
