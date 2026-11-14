FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Ticketory.Worker/Ticketory.Worker.csproj", "src/Ticketory.Worker/"]
COPY ["src/Ticketory.Application/Ticketory.Application.csproj", "src/Ticketory.Application/"]
COPY ["src/Ticketory.Domain/Ticketory.Domain.csproj", "src/Ticketory.Domain/"]
COPY ["src/Ticketory.Infrastructure/Ticketory.Infrastructure.csproj", "src/Ticketory.Infrastructure/"]
RUN dotnet restore "src/Ticketory.Worker/Ticketory.Worker.csproj"

COPY . .
WORKDIR /src/src/Ticketory.Worker
RUN dotnet publish "Ticketory.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN useradd -m -u 10001 appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "Ticketory.Worker.dll"]
