# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Estágio de Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
COPY --from=build-env /app/publish .
# Se o Aggregator também for expor API, mantenha a porta
ENTRYPOINT ["dotnet", "Aggregator.dll"]