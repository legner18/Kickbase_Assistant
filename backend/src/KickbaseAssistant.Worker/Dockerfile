FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY KickbaseAssistant.sln .
COPY src/KickbaseAssistant.Domain/*.csproj src/KickbaseAssistant.Domain/
COPY src/KickbaseAssistant.Infrastructure/*.csproj src/KickbaseAssistant.Infrastructure/
COPY src/KickbaseAssistant.Api/*.csproj src/KickbaseAssistant.Api/
COPY src/KickbaseAssistant.Worker/*.csproj src/KickbaseAssistant.Worker/
RUN dotnet restore src/KickbaseAssistant.Worker/KickbaseAssistant.Worker.csproj

COPY src/ src/
RUN dotnet publish src/KickbaseAssistant.Worker/KickbaseAssistant.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "KickbaseAssistant.Worker.dll"]
