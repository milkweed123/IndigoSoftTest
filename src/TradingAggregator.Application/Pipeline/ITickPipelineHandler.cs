using TradingAggregator.Domain.Entities;

namespace TradingAggregator.Application.Pipeline;

/// <summary>
/// Handler for normalized ticks after passing through the pipeline.
/// </summary>
public interface ITickPipelineHandler
{
    Task HandleAsync(NormalizedTick tick, CancellationToken cancellationToken);
}
