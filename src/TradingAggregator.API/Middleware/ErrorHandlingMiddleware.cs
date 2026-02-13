using System.Net;
using System.Text.Json;
using TradingAggregator.Domain.Exceptions;

namespace TradingAggregator.API.Middleware;

/// <summary>
/// Промежуточное middleware глобальной обработки исключений, которое перехватывает все необработанные исключения
/// и возвращает стандартизированные JSON-ответы с ошибками.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(
            exception,
            "Unhandled exception occurred: {ExceptionType} - {Message}",
            exception.GetType().Name,
            exception.Message);

        var (statusCode, errorResponse) = MapExceptionToResponse(exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await context.Response.WriteAsync(json);
    }

    private static (HttpStatusCode statusCode, ErrorResponse response) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            Domain.Exceptions.EntityNotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ErrorResponse
                {
                    Error = "Not Found",
                    Message = notFoundEx.Message
                }
            ),

            Domain.Exceptions.ExchangeConnectionException connectionEx => (
                HttpStatusCode.ServiceUnavailable,
                new ErrorResponse
                {
                    Error = "Exchange Connection Error",
                    Message = connectionEx.Message
                }
            ),

            Domain.Exceptions.DataProcessingException processingEx => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse
                {
                    Error = "Data Processing Error",
                    Message = processingEx.Message
                }
            ),

            OperationCanceledException => (
                HttpStatusCode.BadRequest,
                new ErrorResponse
                {
                    Error = "Request Cancelled",
                    Message = "The operation was cancelled"
                }
            ),

            ArgumentNullException nullEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse
                {
                    Error = "Missing Argument",
                    Message = nullEx.Message
                }
            ),

            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                new ErrorResponse
                {
                    Error = "Invalid Argument",
                    Message = argEx.Message
                }
            ),

            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse
                {
                    Error = "Internal Server Error",
                    Message = "An unexpected error occurred. Please try again later."
                }
            )
        };
    }
}

/// <summary>
/// Стандартизированный формат ответа с ошибкой.
/// </summary>
public class ErrorResponse
{
    public string Error { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string[]>? Details { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
