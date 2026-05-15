using FluxoCaixa.Operations.API.Endpoints;
using FluxoCaixa.Operations.API.Extensions;
using FluxoCaixa.Operations.API.Middleware;
using FluxoCaixa.Operations.Application;
using FluxoCaixa.Operations.Infrastructure;
using FluxoCaixa.Operations.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

// Bootstrap logger — captura erros de inicialização antes do host estar pronto.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{

var builder = WebApplication.CreateBuilder(args);

// Serilog — JSON estruturado; enrichers; lê MinimumLevel do appsettings.json.
builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(new JsonFormatter()));

var connStr = builder.Configuration.GetConnectionString("Operations")
    ?? throw new InvalidOperationException("Connection string 'Operations' nao configurada.");

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApiRateLimiting();
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddOperationsInfrastructure(connStr);
builder.Services.AddOperationsApplication();
builder.Services.AddOperationsMessaging(builder.Configuration);
builder.Services.AddOperationsObservability();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TransactionDbContext>("operations-db");
builder.Services.AddOpenApi();

var app = builder.Build();

await app.ApplyMigrationsAndSeedAsync();

app.UseSerilogRequestLogging(opts =>
    opts.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} → {StatusCode} em {Elapsed:0.0000} ms");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(opts => opts.WithTitle("FluxoCaixa Operations API"));
app.UseHttpMetrics();
app.MapMetrics("/metrics");
app.MapHealthChecks("/health/live",  new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions());

app.MapAuthEndpoints();
app.MapTransactionEndpoints();

app.Run();

}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Aplicação encerrou inesperadamente durante o startup.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
