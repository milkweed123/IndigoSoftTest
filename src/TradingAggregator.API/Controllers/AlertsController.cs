using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TradingAggregator.Application.DTOs;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.API.Controllers;

/// <summary>
/// Управляет правилами оповещений и предоставляет доступ к истории оповещений.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AlertsController : ControllerBase
{
    private readonly IAlertRuleRepository _alertRuleRepository;
    private readonly IAlertHistoryRepository _alertHistoryRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly ILogger<AlertsController> _logger;
    private readonly IMapper _mapper;

    public AlertsController(
        IAlertRuleRepository alertRuleRepository,
        IAlertHistoryRepository alertHistoryRepository,
        IInstrumentRepository instrumentRepository,
        ILogger<AlertsController> logger,
        IMapper mapper)
    {
        _alertRuleRepository = alertRuleRepository;
        _alertHistoryRepository = alertHistoryRepository;
        _instrumentRepository = instrumentRepository;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Создаёт новое правило оповещения.
    /// </summary>
    /// <param name="dto">Данные для создания правила оповещения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Созданное правило оповещения</returns>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(AlertRuleResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AlertRuleResponseDto>> CreateAlertRule(
        [FromBody] CreateAlertRuleDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { error = "Name is required" });
        }

        if (!Enum.TryParse<AlertRuleType>(dto.RuleType, ignoreCase: true, out var ruleType))
        {
            return BadRequest(new { error = $"Invalid rule type: {dto.RuleType}" });
        }

        try
        {
            var instrument = await _instrumentRepository.GetByIdAsync(dto.InstrumentId, cancellationToken);
            if (instrument is null)
            {
                return BadRequest(new { error = $"Instrument with ID {dto.InstrumentId} not found" });
            }

            var rule = new AlertRule
            {
                Name = dto.Name,
                InstrumentId = dto.InstrumentId,
                RuleType = ruleType,
                Threshold = dto.Threshold,
                PeriodMinutes = dto.PeriodMinutes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _alertRuleRepository.CreateAsync(rule, cancellationToken);

            var response = _mapper.Map<AlertRuleResponseDto>(created);
            response.Symbol = instrument.Symbol;
            response.Exchange = instrument.Exchange.ToString();

            return CreatedAtAction(
                nameof(GetAlertRuleById),
                new { id = created.Id },
                response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for CreateAlertRule");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert rule");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while creating the alert rule" });
        }
    }

    /// <summary>
    /// Получает все активные правила оповещений.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список правил оповещений</returns>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(IEnumerable<AlertRuleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AlertRuleResponseDto>>> GetAllAlertRules(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rules = await _alertRuleRepository.GetAllActiveAsync(cancellationToken);

            var dtos = new List<AlertRuleResponseDto>();

            foreach (var rule in rules)
            {
                var instrument = await _instrumentRepository.GetByIdAsync(rule.InstrumentId, cancellationToken);
                var dto = _mapper.Map<AlertRuleResponseDto>(rule);
                dto.Symbol = instrument?.Symbol;
                dto.Exchange = instrument?.Exchange.ToString();
                dtos.Add(dto);
            }

            return Ok(dtos);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetAllAlertRules");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert rules");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving alert rules" });
        }
    }

    /// <summary>
    /// Получает конкретное правило оповещения по его идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор правила оповещения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Детали правила оповещения</returns>
    [HttpGet("rules/{id}")]
    [ProducesResponseType(typeof(AlertRuleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AlertRuleResponseDto>> GetAlertRuleById(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await _alertRuleRepository.GetByIdAsync(id, cancellationToken);

            if (rule is null)
            {
                return NotFound(new { error = $"Alert rule with ID {id} not found" });
            }

            var instrument = await _instrumentRepository.GetByIdAsync(rule.InstrumentId, cancellationToken);

            var dto = _mapper.Map<AlertRuleResponseDto>(rule);
            dto.Symbol = instrument?.Symbol;
            dto.Exchange = instrument?.Exchange.ToString();

            return Ok(dto);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetAlertRuleById with ID {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert rule with ID {Id}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving the alert rule" });
        }
    }

    /// <summary>
    /// Обновляет существующее правило оповещения.
    /// </summary>
    /// <param name="id">Идентификатор правила оповещения</param>
    /// <param name="dto">Данные для обновления</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Обновлённое правило оповещения</returns>
    [HttpPut("rules/{id}")]
    [ProducesResponseType(typeof(AlertRuleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AlertRuleResponseDto>> UpdateAlertRule(
        int id,
        [FromBody] UpdateAlertRuleDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await _alertRuleRepository.GetByIdAsync(id, cancellationToken);

            if (rule is null)
            {
                return NotFound(new { error = $"Alert rule with ID {id} not found" });
            }

            if (dto.Name is not null)
                rule.Name = dto.Name;

            if (dto.Threshold.HasValue)
                rule.Threshold = dto.Threshold.Value;

            if (dto.PeriodMinutes.HasValue)
                rule.PeriodMinutes = dto.PeriodMinutes;

            if (dto.IsActive.HasValue)
                rule.IsActive = dto.IsActive.Value;

            await _alertRuleRepository.UpdateAsync(rule, cancellationToken);

            var instrument = await _instrumentRepository.GetByIdAsync(rule.InstrumentId, cancellationToken);

            var response = _mapper.Map<AlertRuleResponseDto>(rule);
            response.Symbol = instrument?.Symbol;
            response.Exchange = instrument?.Exchange.ToString();

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for UpdateAlertRule with ID {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alert rule with ID {Id}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while updating the alert rule" });
        }
    }

    /// <summary>
    /// Удаляет правило оповещения.
    /// </summary>
    /// <param name="id">Идентификатор правила оповещения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Пустой результат при успехе</returns>
    [HttpDelete("rules/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAlertRule(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await _alertRuleRepository.GetByIdAsync(id, cancellationToken);

            if (rule is null)
            {
                return NotFound(new { error = $"Alert rule with ID {id} not found" });
            }

            await _alertRuleRepository.DeleteAsync(id, cancellationToken);

            return NoContent();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for DeleteAlertRule with ID {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert rule with ID {Id}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while deleting the alert rule" });
        }
    }

    /// <summary>
    /// Получает историю оповещений с опциональной фильтрацией по временному диапазону.
    /// </summary>
    /// <param name="from">Начальная временная метка (UTC, опционально)</param>
    /// <param name="to">Конечная временная метка (UTC, опционально)</param>
    /// <param name="limit">Максимальное количество результатов (по умолчанию: 100, максимум: 1000)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список записей истории оповещений</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IEnumerable<AlertHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<AlertHistoryDto>>> GetAlertHistory(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || limit > 1000)
        {
            return BadRequest(new { error = "Limit must be between 1 and 1000" });
        }

        try
        {
            var history = await _alertHistoryRepository.GetAsync(
                from: from,
                to: to,
                limit: limit,
                cancellationToken: cancellationToken);

            var dtos = _mapper.Map<List<AlertHistoryDto>>(history);

            return Ok(dtos);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled for GetAlertHistory");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alert history");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving alert history" });
        }
    }
}

/// <summary>
/// Объект передачи данных для записей истории оповещений.
/// </summary>
public class AlertHistoryDto
{
    public long Id { get; init; }
    public int RuleId { get; init; }
    public int InstrumentId { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime TriggeredAt { get; init; }
}
