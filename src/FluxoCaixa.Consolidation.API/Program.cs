using FluxoCaixa.Consolidation.Application;
using FluxoCaixa.Consolidation.Application.Abstractions;
using FluxoCaixa.Consolidation.Application.Consumers;
using FluxoCaixa.Consolidation.Application.Contracts;
using FluxoCaixa.Consolidation.Application.Dtos;
using FluxoCaixa.Consolidation.Application.Queries;
using FluxoCaixa.Consolidation.API.Middleware;
using FluxoCaixa.Consolidation.Domain.Common;
using FluxoCaixa.Consolidation.Infrastructure;
using FluxoCaixa.Consolidation.Infrastructure.Persistence;
using FluxoCaixa.Consolidation.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

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

// JWT
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Segredo JWT nao configurado (Jwt:Secret).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"]   ?? "FluxoCaixa",
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "FluxoCaixa",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// Rate Limiting
builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit        = 600,   // 10 RPS por usuário; NFR de 50 RPS é aggregate (20 VUs × 2,5 RPS)
                Window             = TimeSpan.FromMinutes(1),
            }));

    opts.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Taxa limite excedida. Tente novamente mais tarde.", token);
    };
});

// CORS
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Infrastructure & Application
var connStr = builder.Configuration.GetConnectionString("Consolidation")
    ?? throw new InvalidOperationException("Connection string 'Consolidation' nao configurada.");

builder.Services.AddConsolidationInfrastructure(connStr);
builder.Services.AddConsolidationApplication();

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransactionCreatedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(
            builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            "/",
            h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

        cfg.Message<TransactionCreatedMessage>(m =>
            m.SetEntityName("transaction-created"));

        cfg.ConfigureEndpoints(ctx);
    });
});

// OpenTelemetry — traces (ActivitySource); lê ServiceName do ConsolidationTelemetry.
// O exporter Console é adequado para desenvolvimento local sem coletor externo.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName: ConsolidationTelemetry.ServiceName,
            serviceVersion: ConsolidationTelemetry.ServiceVersion))
    .WithTracing(tracing => tracing
        .AddSource(ConsolidationTelemetry.ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Health Checks — /health/live (sem checks), /health/ready (DB + RabbitMQ via MassTransit).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DailyConsolidationDbContext>("consolidation-db");

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Migrations
await app.ApplyMigrationsAsync();

// Middleware Pipeline
app.UseSerilogRequestLogging(opts =>
    opts.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} → {StatusCode} em {Elapsed:0.0000} ms");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI / Docs
app.MapOpenApi();
app.MapScalarApiReference(opts => opts.WithTitle("FluxoCaixa Consolidation API"));

// Metrics
app.UseHttpMetrics();
app.MapMetrics("/metrics");

// Health
app.MapHealthChecks("/health/live",  new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions());

// Consolidation Endpoints
var consolidation = app.MapGroup("/api/consolidation")
    .WithTags("Consolidation")
    .RequireAuthorization();

consolidation.MapGet("/daily", async (
    [FromQuery] Guid merchantId,
    [FromQuery] DateOnly date,
    HttpContext httpCtx,
    IQueryHandler<GetDailyBalanceQuery, DailyBalanceDto> handler,
    CancellationToken ct) =>
{
    if (!HasMerchantAccess(httpCtx, merchantId))
    {
        return Results.Forbid();
    }

    var result = await handler.HandleAsync(new GetDailyBalanceQuery(merchantId, date), ct);
    return ToHttpResult(result);
})
.WithName("GetDailyBalance")
.WithSummary("Consultar saldo consolidado de um dia");

consolidation.MapGet("/daily/range", async (
    [FromQuery] Guid merchantId,
    [FromQuery] DateOnly from,
    [FromQuery] DateOnly to,
    HttpContext httpCtx,
    IQueryHandler<GetBalanceRangeQuery, IList<DailyBalanceDto>> handler,
    CancellationToken ct) =>
{
    if (!HasMerchantAccess(httpCtx, merchantId))
    {
        return Results.Forbid();
    }

    var result = await handler.HandleAsync(new GetBalanceRangeQuery(merchantId, from, to), ct);
    return ToHttpResult(result);
})
.WithName("GetBalanceRange")
.WithSummary("Consultar saldos consolidados em um intervalo de datas");

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

// Helpers
static bool HasMerchantAccess(HttpContext ctx, Guid requestedMerchantId)
{
    var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
    if (role == "Admin")
    {
        return true;
    }

    var claim = ctx.User.FindFirst("merchant_id")?.Value;
    return Guid.TryParse(claim, out var claimId) && claimId == requestedMerchantId;
}

static IResult MapError(Error error) =>
    error.Type switch
    {
        ErrorType.Validation   => Results.UnprocessableEntity(new { error.Code, error.Message }),
        ErrorType.NotFound     => Results.NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict     => Results.Conflict(new { error.Code, error.Message }),
        ErrorType.Unauthorized => Results.Unauthorized(),
        _                      => Results.Problem(error.Message),
    };

static IResult ToHttpResult<T>(Result<T> result) =>
    result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
