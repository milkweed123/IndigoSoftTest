using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Application.Options;

public sealed class DataSourceOptions
{
    public const string SectionName = "DataSources";

    public List<ExchangeSourceConfig> Exchanges { get; set; } = [];
}

public sealed class ExchangeSourceConfig
{
    public ExchangeType Exchange { get; set; }

    public List<string> Symbols { get; set; } = [];
}
