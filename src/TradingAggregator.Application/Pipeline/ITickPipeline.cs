using System.Threading.Channels;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Application.Pipeline;

/// <summary>
/// Channel-based tick processing pipeline.
/// Exchange adapters write <see cref="RawTick"/> to <see cref="InputWriter"/>.
/// Pipeline normalizes, deduplicates, filters and passes ticks to registered handlers.
/// </summary>
public interface ITickPipeline
{
    /// <summary>
    /// Write endpoint where exchange adapters send raw ticks.
    /// </summary>
    ChannelWriter<RawTick> InputWriter { get; }

    /// <summary>
    /// Registers a handler to receive processed ticks.
    /// Must be called before <see cref="StartAsync"/>.
    /// </summary>
    void RegisterHandler<THandler>() where THandler : ITickPipelineHandler;

    /// <summary>
    /// Starts the background consumption loop.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Signals the pipeline to stop and waits for current work to complete.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
