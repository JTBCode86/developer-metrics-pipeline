# Estágio 1: Build da aplicação usando SDK do .NET 9
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copia o arquivo de projeto e restaura dependências
COPY services/Aggregator/Aggregator.csproj ./services/Aggregator/
RUN dotnet restore ./services/Aggregator/Aggregator.csproj

# Copia o restante dos fontes e compila
COPY services/Aggregator/ ./services/Aggregator/
RUN dotnet publish ./services/Aggregator/Aggregator.csproj -c Release -o /app/publish

# Estágio 2: Imagem final otimizada para Web APIs (.NET 9)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build-env /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Aggregator.dll"]