using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Application.Commands;
using FluxoCaixa.Operations.Application.Dtos;
using FluxoCaixa.Operations.Application.Queries;
using FluxoCaixa.Operations.Domain.Common;
using FluxoCaixa.Operations.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FluxoCaixa.Operations.API.Endpoints;

internal static class AuthEndpoints
{
    internal static WebApplication MapAuthEndpoints(this WebApplication app)
    {
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
        .WithSummary("Autenticar e obter tokens JWT + refresh")
        .AllowAnonymous();

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
        .WithSummary("Renovar token de acesso usando refresh token")
        .AllowAnonymous();

        return app;
    }
}

internal record LoginRequest(string Email, string Password);
internal record RefreshRequest(string RefreshToken);
