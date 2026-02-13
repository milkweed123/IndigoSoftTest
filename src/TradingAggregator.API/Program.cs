using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using TradingAggregator.API.BackgroundServices;
using TradingAggregator.API.Middleware;
using TradingAggregator.Application.Options;
using TradingAggregator.Application.Pipeline;
using TradingAggregator.Application.Services;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;
using TradingAggregator.Domain.Interfaces;
using TradingAggregator.Infrastructure.Alerting;
using TradingAggregator.Infrastructure.Alerting.Channels;
using TradingAggregator.Infrastructure.Alerting.Rules;
using TradingAggregator.Infrastructure.ExchangeAdapters;
using TradingAggregator.Infrastructure.Monitoring;
using TradingAggregator.Infrastructure.Persistence.PostgreSQL;
using TradingAggregator.Infrastructure.Persistence.PostgreSQL.Repositories;
using TradingAggregator.Infrastructure.Persistence.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/tradingaggregator-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Конвертируем enum в строки для JSON (вместо чисел)
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Trading Aggregator API",
        Version = "v1",
        Description = "REST API for accessing aggregated cryptocurrency market data from multiple exchanges"
    });

    // Показываем enum как строки в Swagger UI (dropdown вместо цифр)
    options.UseInlineDefinitionsForEnums();

    // Добавляем XML комментарии для лучшей документации
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddHttpClient();

builder.Services.AddAutoMapper(typeof(TradingAggregator.Application.Mappings.MappingProfile));

builder.Services.Configure<DataSourceOptions>(
    builder.Configuration.GetSection(DataSourceOptions.SectionName));
builder.Services.Configure<AggregationOptions>(
    builder.Configuration.GetSection(AggregationOptions.SectionName));
builder.Services.Configure<AlertOptions>(
    builder.Configuration.GetSection(AlertOptions.SectionName));

var postgresConnectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("PostgreSQL connection string not found");

builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(postgresConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    }));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string not found");

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();
builder.Services.AddScoped<ITickRepository, TickRepository>();
builder.Services.AddScoped<ICandleRepository, CandleRepository>();
builder.Services.AddScoped<IExchangeStatusRepository, ExchangeStatusRepository>();
builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
builder.Services.AddScoped<IAlertHistoryRepository, AlertHistoryRepository>();

builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
builder.Services.AddSingleton<IDeduplicator>(sp =>
{
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<RedisDeduplicator>>();
    return new RedisDeduplicator(redis, logger);
});

builder.Services.AddSingleton<RedisTickBuffer>(sp =>
{
    var tickRepo = sp.CreateScope().ServiceProvider.GetRequiredService<ITickRepository>();
    var logger = sp.GetRequiredService<ILogger<RedisTickBuffer>>();
    return new RedisTickBuffer(tickRepo, logger, batchSize: 500, flushInterval: TimeSpan.FromSeconds(5));
});

builder.Services.AddSingleton<InstrumentFilter>();
builder.Services.AddSingleton<ITickPipeline>(sp =>
{
    var serviceProvider = sp;
    var instrumentFilter = sp.GetRequiredService<InstrumentFilter>();
    var deduplicator = sp.GetRequiredService<IDeduplicator>();
    var metricsCollector = sp.GetRequiredService<IMetricsCollector>();
    var logger = sp.GetRequiredService<ILogger<TickPipeline>>();

    var pipeline = new TickPipeline(serviceProvider, instrumentFilter, deduplicator, metricsCollector, logger);

    pipeline.RegisterHandler<DataAggregationService>();
    pipeline.RegisterHandler<AlertService>();

    return pipeline;
});

builder.Services.AddSingleton<ExchangeAdapterFactory>();

builder.Services.AddSingleton<IAlertRuleEvaluator, PriceThresholdRule>();
builder.Services.AddSingleton<IAlertRuleEvaluator, PriceChangeRule>();
builder.Services.AddSingleton<IAlertRuleEvaluator, VolumeChangeRule>();
builder.Services.AddSingleton<IAlertRuleEvaluator, VolatilityRule>();

builder.Services.AddSingleton<INotificationChannel, ConsoleNotifier>();
builder.Services.AddSingleton<INotificationChannel, FileNotifier>();
builder.Services.AddSingleton<INotificationChannel, EmailNotifierStub>();

builder.Services.AddSingleton<DataAggregationService>(sp =>
{
    var candleRepo = sp.CreateScope().ServiceProvider.GetRequiredService<ICandleRepository>();
    var tickRepo = sp.CreateScope().ServiceProvider.GetRequiredService<ITickRepository>();
    var instrumentRepo = sp.CreateScope().ServiceProvider.GetRequiredService<IInstrumentRepository>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AggregationOptions>>();
    var logger = sp.GetRequiredService<ILogger<DataAggregationService>>();
    return new DataAggregationService(candleRepo, tickRepo, instrumentRepo, options, logger);
});

builder.Services.AddSingleton<AlertService>(sp =>
{
    var alertRuleRepo = sp.CreateScope().ServiceProvider.GetRequiredService<IAlertRuleRepository>();
    var alertHistoryRepo = sp.CreateScope().ServiceProvider.GetRequiredService<IAlertHistoryRepository>();
    var instrumentRepo = sp.CreateScope().ServiceProvider.GetRequiredService<IInstrumentRepository>();
    var evaluators = sp.GetRequiredService<IEnumerable<IAlertRuleEvaluator>>();
    var channels = sp.GetRequiredService<IEnumerable<INotificationChannel>>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AlertOptions>>();
    var logger = sp.GetRequiredService<ILogger<AlertService>>();
    return new AlertService(alertRuleRepo, alertHistoryRepo, instrumentRepo, evaluators, channels, options, logger);
});

builder.Services.AddScoped<MonitoringService>();

builder.Services.AddHostedService<ExchangeDataCollectorService>();
builder.Services.AddHostedService<DataAggregatorService>();
builder.Services.AddHostedService<MonitoringBackgroundService>();
builder.Services.AddHostedService<DataRetentionService>();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Trading Aggregator API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await dbContext.Database.MigrateAsync();

    await SeedInstrumentsAsync(dbContext, logger);
;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Error while initializating the database");
    throw;
}

app.Run();

static async Task SeedInstrumentsAsync(TradingDbContext dbContext, Microsoft.Extensions.Logging.ILogger logger)
{
    if (await dbContext.Instruments.AnyAsync())
    {
        logger.LogInformation("Instruments already exist, skipping seed");
        return;
    }

    var instruments = new List<Instrument>
    {
        new() { Symbol = "BTCUSDT", Exchange = ExchangeType.Binance, BaseCurrency = "BTC", QuoteCurrency = "USDT", IsActive = true },
        new() { Symbol = "ETHUSDT", Exchange = ExchangeType.Binance, BaseCurrency = "ETH", QuoteCurrency = "USDT", IsActive = true },
        new() { Symbol = "BTCUSDT", Exchange = ExchangeType.Bybit, BaseCurrency = "BTC", QuoteCurrency = "USDT", IsActive = true },
        new() { Symbol = "ETHUSDT", Exchange = ExchangeType.Bybit, BaseCurrency = "ETH", QuoteCurrency = "USDT", IsActive = true },
        new() { Symbol = "BTCUSDT", Exchange = ExchangeType.Okx, BaseCurrency = "BTC", QuoteCurrency = "USDT", IsActive = true },
        new() { Symbol = "ETHUSDT", Exchange = ExchangeType.Okx, BaseCurrency = "ETH", QuoteCurrency = "USDT", IsActive = true }
    };

    await dbContext.Instruments.AddRangeAsync(instruments);
    await dbContext.SaveChangesAsync();

    logger.LogInformation("Added {Count} initial instruments", instruments.Count);
}
