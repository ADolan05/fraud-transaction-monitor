# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FraudMonitor.Api/FraudMonitor.Api.csproj FraudMonitor.Api/
RUN dotnet restore FraudMonitor.Api/FraudMonitor.Api.csproj
COPY FraudMonitor.Api/ FraudMonitor.Api/
RUN dotnet publish FraudMonitor.Api/FraudMonitor.Api.csproj -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "FraudMonitor.Api.dll"]
