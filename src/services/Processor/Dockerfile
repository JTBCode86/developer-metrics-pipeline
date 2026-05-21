# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Estágio de Runtime (Imagem correta com suporte ao ASP.NET)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
COPY --from=build-env /app/publish .
# Como é uma API, o Kestrel escuta na 8080 por padrão
EXPOSE 8080
ENTRYPOINT ["dotnet", "Processor.dll"]