FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/TradingAggregator.Domain/TradingAggregator.Domain.csproj", "src/TradingAggregator.Domain/"]
COPY ["src/TradingAggregator.Application/TradingAggregator.Application.csproj", "src/TradingAggregator.Application/"]
COPY ["src/TradingAggregator.Infrastructure/TradingAggregator.Infrastructure.csproj", "src/TradingAggregator.Infrastructure/"]
COPY ["src/TradingAggregator.API/TradingAggregator.API.csproj", "src/TradingAggregator.API/"]
RUN dotnet restore "src/TradingAggregator.API/TradingAggregator.API.csproj"

COPY src/ src/
WORKDIR "/src/src/TradingAggregator.API"
RUN dotnet build "TradingAggregator.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TradingAggregator.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TradingAggregator.API.dll"]
