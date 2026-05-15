using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Application.Commands;
using FluxoCaixa.Operations.Application.Dtos;
using FluxoCaixa.Operations.Application.Queries;
using FluxoCaixa.Operations.Domain.Common;
using FluxoCaixa.Operations.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FluxoCaixa.Operations.API.Endpoints;

internal static class TransactionEndpoints
{
    internal static WebApplication MapTransactionEndpoints(this WebApplication app)
    {
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

            OperationsTelemetry.TransactionsCreated.Add(1);

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

internal record CreateTransactionRequest(
    Guid     MerchantId,
    string   Type,
    decimal  Amount,
    string   Currency,
    string?  Description,
    DateTime OccurredAt);
