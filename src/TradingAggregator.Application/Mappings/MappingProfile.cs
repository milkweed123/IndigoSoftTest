using AutoMapper;
using TradingAggregator.Application.DTOs;
using TradingAggregator.Domain.Entities;
using TradingAggregator.Domain.Enums;

namespace TradingAggregator.Application.Mappings;

/// <summary>
/// AutoMapper mapping profile for converting between entities and DTOs
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Tick, TickDto>()
            .ForMember(dest => dest.Exchange, opt => opt.MapFrom(src => src.Exchange.ToString()))
            .ForMember(dest => dest.SourceType, opt => opt.MapFrom(src => src.SourceType.ToString()));

        CreateMap<Candle, CandleDto>()
            .ForMember(dest => dest.Symbol, opt => opt.MapFrom(src => src.Instrument != null ? src.Instrument.Symbol : string.Empty))
            .ForMember(dest => dest.Exchange, opt => opt.MapFrom(src => src.Instrument != null ? src.Instrument.Exchange.ToString() : string.Empty))
            .ForMember(dest => dest.Interval, opt => opt.MapFrom(src => src.Interval.ToString()));

        CreateMap<AlertRule, AlertRuleResponseDto>()
            .ForMember(dest => dest.Symbol, opt => opt.MapFrom(src => src.Instrument != null ? src.Instrument.Symbol : null))
            .ForMember(dest => dest.Exchange, opt => opt.MapFrom(src => src.Instrument != null ? src.Instrument.Exchange.ToString() : null))
            .ForMember(dest => dest.RuleType, opt => opt.MapFrom(src => src.RuleType.ToString()));

        CreateMap<CreateAlertRuleDto, AlertRule>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Instrument, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
            .ForMember(dest => dest.RuleType, opt => opt.MapFrom(src => Enum.Parse<AlertRuleType>(src.RuleType, true)));

        CreateMap<UpdateAlertRuleDto, AlertRule>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.InstrumentId, opt => opt.Ignore())
            .ForMember(dest => dest.RuleType, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Instrument, opt => opt.Ignore())
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<AlertHistory, AlertHistoryDto>();

        CreateMap<Instrument, InstrumentDto>()
            .ForMember(dest => dest.Exchange, opt => opt.MapFrom(src => src.Exchange.ToString()));
    }
}

/// <summary>
/// DTO for alert history
/// </summary>
public class AlertHistoryDto
{
    public long Id { get; set; }
    public int RuleId { get; set; }
    public int InstrumentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
}

/// <summary>
/// DTO for instrument metadata
/// </summary>
public class InstrumentDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
