# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY *.sln ./
COPY src/ ./src/

RUN dotnet restore "src/services/Aggregator/Aggregator.csproj"
RUN dotnet test "src/tests/DeveloperMetrics.Tests/DeveloperMetrics.Tests.csproj" --logger "console;verbosity=normal"

# Publica na pasta /out
RUN dotnet publish "src/services/Aggregator/Aggregator.csproj" -c Release -o /out

# Estágio Final (Corrigido para ASPNET)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
# Adicionado o comando COPY que estava faltando
COPY --from=build /out .
ENTRYPOINT ["dotnet", "Aggregator.dll"]