# syntax=docker/dockerfile:1.7

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Directory.Build.props (propriedades compartilhadas por todos os projetos)
COPY Directory.Build.props .

# Copia apenas os arquivos .csproj para maximizar o cache de restore
COPY src/FluxoCaixa.Consolidation.Domain/FluxoCaixa.Consolidation.Domain.csproj               src/FluxoCaixa.Consolidation.Domain/
COPY src/FluxoCaixa.Consolidation.Application/FluxoCaixa.Consolidation.Application.csproj     src/FluxoCaixa.Consolidation.Application/
COPY src/FluxoCaixa.Consolidation.Infrastructure/FluxoCaixa.Consolidation.Infrastructure.csproj src/FluxoCaixa.Consolidation.Infrastructure/
COPY src/FluxoCaixa.Consolidation.API/FluxoCaixa.Consolidation.API.csproj                      src/FluxoCaixa.Consolidation.API/

RUN dotnet restore src/FluxoCaixa.Consolidation.API/FluxoCaixa.Consolidation.API.csproj

# Copia o código-fonte e publica
COPY src/FluxoCaixa.Consolidation.Domain/      src/FluxoCaixa.Consolidation.Domain/
COPY src/FluxoCaixa.Consolidation.Application/ src/FluxoCaixa.Consolidation.Application/
COPY src/FluxoCaixa.Consolidation.Infrastructure/ src/FluxoCaixa.Consolidation.Infrastructure/
COPY src/FluxoCaixa.Consolidation.API/         src/FluxoCaixa.Consolidation.API/

RUN dotnet publish src/FluxoCaixa.Consolidation.API/FluxoCaixa.Consolidation.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Usa usuário não-root incluído nas imagens .NET 8+ (uid=1654)
COPY --from=build --chown=app:app /app/publish .
USER app

ENTRYPOINT ["dotnet", "FluxoCaixa.Consolidation.API.dll"]
