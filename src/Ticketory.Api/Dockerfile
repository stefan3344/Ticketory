FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Ticketory.Api/Ticketory.Api.csproj", "src/Ticketory.Api/"]
COPY ["src/Ticketory.Application/Ticketory.Application.csproj", "src/Ticketory.Application/"]
COPY ["src/Ticketory.Domain/Ticketory.Domain.csproj", "src/Ticketory.Domain/"]
COPY ["src/Ticketory.Infrastructure/Ticketory.Infrastructure.csproj", "src/Ticketory.Infrastructure/"]
RUN dotnet restore "src/Ticketory.Api/Ticketory.Api.csproj"

COPY . .
WORKDIR /src/src/Ticketory.Api
RUN dotnet publish "Ticketory.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN useradd -m -u 10001 appuser && chown -R appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Ticketory.Api.dll"]
