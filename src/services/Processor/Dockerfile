# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY *.sln ./
COPY src/ ./src/

RUN dotnet restore "src/services/Processor/Processor.csproj"
RUN dotnet test "src/tests/DeveloperMetrics.Tests/DeveloperMetrics.Tests.csproj" --logger "console;verbosity=normal"

# Publica na pasta /out
RUN dotnet publish "src/services/Processor/Processor.csproj" -c Release -o /out

# Estágio Final (ASPNET é obrigatório para Web)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
# Copia o que foi publicado em /out
COPY --from=build /out .
ENTRYPOINT ["dotnet", "Processor.dll"]