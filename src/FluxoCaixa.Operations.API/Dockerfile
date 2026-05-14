# syntax=docker/dockerfile:1.7

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Directory.Build.props (propriedades compartilhadas por todos os projetos)
COPY Directory.Build.props .

# Copia apenas os arquivos .csproj para maximizar o cache de restore
COPY src/FluxoCaixa.Operations.Domain/FluxoCaixa.Operations.Domain.csproj               src/FluxoCaixa.Operations.Domain/
COPY src/FluxoCaixa.Operations.Application/FluxoCaixa.Operations.Application.csproj     src/FluxoCaixa.Operations.Application/
COPY src/FluxoCaixa.Operations.Infrastructure/FluxoCaixa.Operations.Infrastructure.csproj src/FluxoCaixa.Operations.Infrastructure/
COPY src/FluxoCaixa.Operations.API/FluxoCaixa.Operations.API.csproj                      src/FluxoCaixa.Operations.API/

RUN dotnet restore src/FluxoCaixa.Operations.API/FluxoCaixa.Operations.API.csproj

# Copia o código-fonte e publica
COPY src/FluxoCaixa.Operations.Domain/      src/FluxoCaixa.Operations.Domain/
COPY src/FluxoCaixa.Operations.Application/ src/FluxoCaixa.Operations.Application/
COPY src/FluxoCaixa.Operations.Infrastructure/ src/FluxoCaixa.Operations.Infrastructure/
COPY src/FluxoCaixa.Operations.API/         src/FluxoCaixa.Operations.API/

RUN dotnet publish src/FluxoCaixa.Operations.API/FluxoCaixa.Operations.API.csproj \
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

ENTRYPOINT ["dotnet", "FluxoCaixa.Operations.API.dll"]
