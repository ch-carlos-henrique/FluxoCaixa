using FluxoCaixa.Consolidation.Application.Abstractions;
using FluxoCaixa.Consolidation.Application.Dtos;
using FluxoCaixa.Consolidation.Application.Queries;
using FluxoCaixa.Consolidation.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FluxoCaixa.Consolidation.API.Endpoints;

internal static class ConsolidationEndpoints
{
    internal static WebApplication MapConsolidationEndpoints(this WebApplication app)
    {
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

        return app;
    }

    private static bool HasMerchantAccess(HttpContext ctx, Guid requestedMerchantId)
    {
        var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Admin")
        {
            return true;
        }

        var claim = ctx.User.FindFirst("merchant_id")?.Value;
        return Guid.TryParse(claim, out var claimId) && claimId == requestedMerchantId;
    }

    private static IResult MapError(Error error) =>
        error.Type switch
        {
            ErrorType.Validation   => Results.UnprocessableEntity(new { error.Code, error.Message }),
            ErrorType.NotFound     => Results.NotFound(new { error.Code, error.Message }),
            ErrorType.Conflict     => Results.Conflict(new { error.Code, error.Message }),
            ErrorType.Unauthorized => Results.Unauthorized(),
            _                      => Results.Problem(error.Message),
        };

    private static IResult ToHttpResult<T>(Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
}
