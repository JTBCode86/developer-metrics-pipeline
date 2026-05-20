# Estágio 1: Build da aplicação usando SDK do .NET 9
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copia os arquivos de projeto (.csproj) para restaurar as dependências
COPY services/Processor/Processor.csproj ./services/Processor/
RUN dotnet restore ./services/Processor/Processor.csproj

# Copia o restante dos fontes e compila em modo Release
COPY services/Processor/ ./services/Processor/
RUN dotnet publish ./services/Processor/Processor.csproj -c Release -o /app/publish

# Estágio 2: Imagem final de execução ultra-leve (Runtime .NET 9)
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine
WORKDIR /app
COPY --from=build-env /app/publish .

ENTRYPOINT ["dotnet", "Processor.dll"]