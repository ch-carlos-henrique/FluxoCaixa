using FluxoCaixa.Operations.Application;
using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Application.Commands;
using FluxoCaixa.Operations.Application.Dtos;
using FluxoCaixa.Operations.Application.Queries;
using FluxoCaixa.Operations.API.Middleware;
using FluxoCaixa.Operations.Domain.Common;
using FluxoCaixa.Operations.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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
                PermitLimit        = 200,
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
var connStr = builder.Configuration.GetConnectionString("Operations")
    ?? throw new InvalidOperationException("Connection string 'Operations' nao configurada.");

builder.Services.AddOperationsInfrastructure(connStr);
builder.Services.AddOperationsApplication();

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
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

        cfg.Message<FluxoCaixa.Operations.Application.Contracts.TransactionCreatedMessage>(m =>
            m.SetEntityName("transaction-created"));

        cfg.ConfigureEndpoints(ctx);
    });
});

// Health Checks
builder.Services.AddHealthChecks();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Migrations + Seed
await app.ApplyMigrationsAndSeedAsync();

// Middleware Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI / Docs
app.MapOpenApi();
app.MapScalarApiReference(opts => opts.WithTitle("FluxoCaixa Operations API"));

// Metrics
app.UseHttpMetrics();
app.MapMetrics("/metrics");

// Health
app.MapHealthChecks("/health/live",  new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions());

// Auth Endpoints
var auth = app.MapGroup("/api/auth").WithTags("Auth");

auth.MapPost("/login", async (
    [FromBody] LoginRequest req,
    IAuthService authService,
    CancellationToken ct) =>
{
    var result = await authService.LoginAsync(req.Email, req.Password, ct);
    return result is null
        ? Results.Unauthorized()
        : Results.Ok(result);
})
.WithName("Login")
.WithSummary("Autenticar e obter tokens JWT + refresh");

auth.MapPost("/refresh", async (
    [FromBody] RefreshRequest req,
    IAuthService authService,
    CancellationToken ct) =>
{
    var result = await authService.RefreshAsync(req.RefreshToken, ct);
    return result is null
        ? Results.Unauthorized()
        : Results.Ok(result);
})
.WithName("RefreshToken")
.WithSummary("Renovar token de acesso usando refresh token");

// Transaction Endpoints
var transactions = app.MapGroup("/api/transactions")
    .WithTags("Transactions")
    .RequireAuthorization();

transactions.MapPost("/", async (
    [FromBody] CreateTransactionRequest req,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    HttpContext httpCtx,
    ICommandHandler<CreateTransactionCommand, TransactionDto> handler,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return Results.BadRequest(new { error = "O header 'Idempotency-Key' e obrigatorio." });
    }

    if (!HasMerchantAccess(httpCtx, req.MerchantId))
    {
        return Results.Forbid();
    }

    var command = new CreateTransactionCommand(
        req.MerchantId,
        req.Type,
        req.Amount,
        req.Currency,
        req.Description,
        req.OccurredAt,
        idempotencyKey);

    var result = await handler.HandleAsync(command, ct);

    if (result.IsFailure)
    {
        return MapError(result.Error);
    }

    return Results.Created($"/api/transactions/{result.Value.Id}", result.Value);
})
.WithName("CreateTransaction")
.WithSummary("Criar lancamento (credito ou debito)");

transactions.MapGet("/{id:guid}", async (
    Guid id,
    HttpContext httpCtx,
    IQueryHandler<GetTransactionByIdQuery, TransactionDto> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new GetTransactionByIdQuery(id), ct);

    if (result.IsSuccess && !HasMerchantAccess(httpCtx, result.Value.MerchantId))
    {
        return Results.Forbid();
    }

    return ToHttpResult(result);
})
.WithName("GetTransactionById")
.WithSummary("Buscar lancamento por ID");

transactions.MapGet("/", async (
    [FromQuery] Guid merchantId,
    [FromQuery] DateTime from,
    [FromQuery] DateTime to,
    HttpContext httpCtx,
    IQueryHandler<GetTransactionsByDateQuery, IList<TransactionDto>> handler,
    CancellationToken ct) =>
{
    if (!HasMerchantAccess(httpCtx, merchantId))
    {
        return Results.Forbid();
    }

    var result = await handler.HandleAsync(
        new GetTransactionsByDateQuery(merchantId, from, to), ct);

    return ToHttpResult(result);
})
.WithName("GetTransactions")
.WithSummary("Listar lancamentos por comerciante e periodo");

app.Run();

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

// Request Records
record LoginRequest(string Email, string Password);
record RefreshRequest(string RefreshToken);
record CreateTransactionRequest(
    Guid     MerchantId,
    string   Type,
    decimal  Amount,
    string   Currency,
    string?  Description,
    DateTime OccurredAt);
