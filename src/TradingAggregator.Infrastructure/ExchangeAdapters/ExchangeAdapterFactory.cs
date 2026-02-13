using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingAggregator.Application.Options;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;

namespace TradingAggregator.Infrastructure.ExchangeAdapters;

public class ExchangeAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly DataSourceOptions _dataSourceOptions;
    private readonly ILogger<ExchangeAdapterFactory> _logger;

    public ExchangeAdapterFactory(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IOptions<DataSourceOptions> dataSourceOptions,
        ILogger<ExchangeAdapterFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _dataSourceOptions = dataSourceOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates all enabled exchange adapters based on configuration.
    ///
    /// Expected configuration section:
    /// "ExchangeAdapters": {
    ///   "BinanceWebSocket": { "Enabled": true },
    ///   "BinanceRest": { "Enabled": true, "PollingIntervalSeconds": 5 },
    ///   "BybitWebSocket": { "Enabled": true },
    ///   "OkxWebSocket": { "Enabled": true }
    /// }
    /// </summary>
    public IReadOnlyList<IExchangeAdapter> CreateAdapters()
    {
        var adapters = new List<IExchangeAdapter>();
        var section = _configuration.GetSection("ExchangeAdapters");

        if (IsAdapterEnabled(section, "BinanceWebSocket"))
        {
            var adapter = CreateBinanceWebSocketAdapter();
            adapters.Add(adapter);
            _logger.LogInformation("Binance WebSocket adapter enabled");
        }

        if (IsAdapterEnabled(section, "BinanceRest"))
        {
            var pollingInterval = GetPollingInterval(section, "BinanceRest", defaultSeconds: 5);
            var adapter = CreateBinanceRestAdapter(pollingInterval);
            adapters.Add(adapter);
            _logger.LogInformation("Binance REST adapter enabled with {Interval}s interval",
                pollingInterval.TotalSeconds);
        }

        if (IsAdapterEnabled(section, "BybitWebSocket"))
        {
            var adapter = CreateBybitWebSocketAdapter();
            adapters.Add(adapter);
            _logger.LogInformation("Bybit WebSocket adapter enabled");
        }

        if (IsAdapterEnabled(section, "BybitRest"))
        {
            var pollingInterval = GetPollingInterval(section, "BybitRest", defaultSeconds: 5);
            var adapter = CreateBybitRestAdapter(pollingInterval);
            adapters.Add(adapter);
            _logger.LogInformation("Bybit REST adapter enabled with {Interval}s interval",
                pollingInterval.TotalSeconds);
        }

        if (IsAdapterEnabled(section, "OkxWebSocket"))
        {
            var adapter = CreateOkxWebSocketAdapter();
            adapters.Add(adapter);
            _logger.LogInformation("OKX WebSocket adapter enabled");
        }

        if (IsAdapterEnabled(section, "OkxRest"))
        {
            var pollingInterval = GetPollingInterval(section, "OkxRest", defaultSeconds: 5);
            var adapter = CreateOkxRestAdapter(pollingInterval);
            adapters.Add(adapter);
            _logger.LogInformation("OKX REST adapter enabled with {Interval}s interval",
                pollingInterval.TotalSeconds);
        }

        if (adapters.Count == 0)
        {
            _logger.LogWarning("No exchange adapters are enabled. Check configuration section 'ExchangeAdapters'.");
        }
        else
        {
            _logger.LogInformation("Created {Count} exchange adapter(s): {Adapters}",
                adapters.Count,
                string.Join(", ", adapters.Select(a => $"{a.Exchange}:{a.SourceType}")));
        }

        return adapters.AsReadOnly();
    }

    private BinanceWebSocketAdapter CreateBinanceWebSocketAdapter()
    {
        var symbols = GetSymbolsForExchange(ExchangeType.Binance);
        return new BinanceWebSocketAdapter(
            _serviceProvider.GetRequiredService<ILogger<BinanceWebSocketAdapter>>(),
            _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Binance"),
            symbols);
    }

    private BinanceRestAdapter CreateBinanceRestAdapter(TimeSpan pollingInterval)
    {
        var symbols = GetSymbolsForExchange(ExchangeType.Binance);
        return new BinanceRestAdapter(
            _serviceProvider.GetRequiredService<ILogger<BinanceRestAdapter>>(),
            _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("BinanceRest"),
            pollingInterval,
            symbols);
    }

    private BybitWebSocketAdapter CreateBybitWebSocketAdapter()
    {
        var symbols = GetSymbolsForExchange(ExchangeType.Bybit);
        return new BybitWebSocketAdapter(
            _serviceProvider.GetRequiredService<ILogger<BybitWebSocketAdapter>>(),
            _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Bybit"),
            symbols);
    }

    private BybitRestAdapter CreateBybitRestAdapter(TimeSpan pollingInterval)
    {
        var symbols = GetSymbolsForExchange(ExchangeType.Bybit);
        return new BybitRestAdapter(
            _serviceProvider.GetRequiredService<ILogger<BybitRestAdapter>>(),
            _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("BybitRest"),
            pollingInterval,
            symbols);
    }

    private OkxWebSocketAdapter CreateOkxWebSocketAdapter()
    {
        var symbols = GetSymbolsForExchange(ExchangeType.Okx);
        return new OkxWebSocketAdapter(
            _serviceProvider.GetRequiredService<ILogger<OkxWebSocketAdapter>>(),
            _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Okx"),
            symbols);
    }

    private OkxRestAdapter CreateOkxRestAdapter(TimeSpan pollingInterval)
    {
        var symbols = GetSymbolsForExchange(ExchangeType.Okx);
        return new OkxRestAdapter(
            _serviceProvider.GetRequiredService<ILogger<OkxRestAdapter>>(),
            _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("OkxRest"),
            pollingInterval,
            symbols);
    }

    private IReadOnlyList<string> GetSymbolsForExchange(ExchangeType exchange)
    {
        var exchangeConfig = _dataSourceOptions.Exchanges
            .FirstOrDefault(e => e.Exchange == exchange);

        if (exchangeConfig == null || exchangeConfig.Symbols.Count == 0)
        {
            _logger.LogWarning(
                "No symbols configured for {Exchange}, using empty list",
                exchange);
            return Array.Empty<string>();
        }

        return exchangeConfig.Symbols.AsReadOnly();
    }

    private static bool IsAdapterEnabled(IConfigurationSection section, string adapterName)
    {
        var adapterSection = section.GetSection(adapterName);
        if (!adapterSection.Exists())
            return false;

        return adapterSection.GetValue<bool>("Enabled", defaultValue: false);
    }

    private static TimeSpan GetPollingInterval(
        IConfigurationSection section,
        string adapterName,
        int defaultSeconds)
    {
        var adapterSection = section.GetSection(adapterName);
        var seconds = adapterSection.GetValue<int>("PollingIntervalSeconds", defaultValue: defaultSeconds);
        return TimeSpan.FromSeconds(Math.Max(1, seconds));
    }
}
