using Microsoft.Extensions.Logging;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.Alerting.Channels;

public sealed class FileNotifier : INotificationChannel, IDisposable
{
    private readonly ILogger<FileNotifier> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _filePath;

    public string Name => "File";

    public FileNotifier(ILogger<FileNotifier> logger)
    {
        _logger = logger;
        _filePath = Path.Combine("logs", "alerts.log");
    }

    public async Task SendAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

        await _writeLock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_filePath, line, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger.LogDebug("File notification written to {FilePath}: {Message}", _filePath, message);
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
