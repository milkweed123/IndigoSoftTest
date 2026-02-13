namespace TradingAggregator.Domain.Exceptions;

public class ExchangeConnectionException : Exception
{
    public ExchangeConnectionException(string exchange, string message, Exception? innerException = null)
        : base($"[{exchange}] Connection error: {message}", innerException) { }
}

public class DataProcessingException : Exception
{
    public DataProcessingException(string message, Exception? innerException = null)
        : base($"Data processing error: {message}", innerException) { }
}

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityType, object id)
        : base($"{entityType} with id '{id}' was not found") { }
}
