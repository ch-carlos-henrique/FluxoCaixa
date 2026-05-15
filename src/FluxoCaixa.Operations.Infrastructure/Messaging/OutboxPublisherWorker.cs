using FluxoCaixa.Operations.Application.Contracts;
using FluxoCaixa.Operations.Infrastructure.Persistence;
using FluxoCaixa.Operations.Infrastructure.Telemetry;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Resilience;
using Polly;
using Polly.Retry;
using System.Text.Json;

namespace FluxoCaixa.Operations.Infrastructure.Messaging;

/// <summary>
/// Worker em background que lê mensagens pendentes do outbox e publica via MassTransit.
/// Garante a entrega de eventos mesmo que o broker esteja temporariamente indisponível.
///
/// Resiliência: Polly v8 — retry 3x com backoff exponencial (2s, 4s, 8s).
/// Após 3 falhas consecutivas na publicação, a mensagem é marcada como "Failed".
///
/// Concorrência (múltiplas réplicas): o worker usa UPDATE ... WHERE status = 'Pending' ...
/// RETURNING com FOR UPDATE SKIP LOCKED para reivindicar mensagens atomicamente.
/// Mensagens reivindicadas ficam em "Processing" durante a publicação, impedindo que
/// outras réplicas as processem em paralelo. Um passo de recuperação reseta mensagens
/// presas em "Processing" por mais de <see cref="ProcessingTimeout"/> de volta a "Pending".
/// </summary>
public sealed class OutboxPublisherWorker : BackgroundService
{
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(5);

    private static readonly ResiliencePipeline _retryPipeline =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                UseJitter = true,
            })
            .Build();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        IPublishEndpoint publishEndpoint,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStuckMessagesAsync(stoppingToken);
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Erro inesperado no OutboxPublisherWorker.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxPublisherWorker encerrado.");
    }

    /// <summary>
    /// Reseta para "Pending" mensagens que estão em "Processing" há mais de
    /// <see cref="ProcessingTimeout"/>. Isso recupera mensagens de réplicas que
    /// falharam durante a publicação sem atualizar o status.
    /// </summary>
    private async Task RecoverStuckMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();

        var cutoff = DateTime.UtcNow - ProcessingTimeout;
        var recovered = await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE outbox_messages
               SET status = 'Pending', processing_started_at = NULL
             WHERE status = 'Processing'
               AND processing_started_at < {0}
            """,
            [cutoff],
            cancellationToken);

        if (recovered > 0)
        {
            _logger.LogWarning(
                "Recuperadas {Count} mensagem(ns) travadas em 'Processing' (timeout {Timeout}min).",
                recovered, ProcessingTimeout.TotalMinutes);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();

        // Reivindica mensagens atomicamente usando FOR UPDATE SKIP LOCKED, evitando que
        // múltiplas réplicas do worker processem a mesma mensagem simultaneamente.
        // UPDATE ... RETURNING * é rastreado pelo EF Core/Npgsql como entidades normais.
        var claimedMessages = await dbContext.OutboxMessages
            .FromSqlRaw(
                """
                UPDATE outbox_messages
                   SET status = 'Processing',
                       processing_started_at = NOW() AT TIME ZONE 'UTC'
                 WHERE id IN (
                     SELECT id
                       FROM outbox_messages
                      WHERE status = 'Pending'
                      ORDER BY created_at
                      LIMIT 50
                        FOR UPDATE SKIP LOCKED
                 )
                 RETURNING *
                """)
            .ToListAsync(cancellationToken);

        // Atualiza o gauge de mensagens pendentes (lido pelo OTel a cada coleta).
        OperationsTelemetry.SetOutboxPendingCount(claimedMessages.Count);

        if (claimedMessages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processando {Count} mensagem(ns) reivindicada(s) do outbox.", claimedMessages.Count);

        foreach (var outboxMessage in claimedMessages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await _retryPipeline.ExecuteAsync(
                    async ct => await PublishMessageAsync(outboxMessage, ct),
                    cancellationToken);

                outboxMessage.Status = "Published";
                outboxMessage.PublishedAt = DateTime.UtcNow;
                outboxMessage.ProcessingStartedAt = null;

                OperationsTelemetry.OutboxMessagesPublished.Add(1);

                _logger.LogInformation(
                    "Mensagem {MessageId} ({EventType}) publicada com sucesso.",
                    outboxMessage.Id, outboxMessage.EventType);
            }
            catch (Exception ex)
            {
                outboxMessage.RetryCount++;

                if (outboxMessage.RetryCount >= MaxRetryAttempts)
                {
                    // Esgotou as tentativas — move para Failed.
                    outboxMessage.Status = "Failed";
                    outboxMessage.ProcessingStartedAt = null;

                    OperationsTelemetry.OutboxMessagesFailed.Add(1);

                    _logger.LogError(
                        ex,
                        "Mensagem {MessageId} ({EventType}) falhou após {MaxRetries} tentativas. Status: Failed.",
                        outboxMessage.Id, outboxMessage.EventType, MaxRetryAttempts);
                }
                else
                {
                    // Devolve para Pending para ser reprocessada no próximo ciclo.
                    outboxMessage.Status = "Pending";
                    outboxMessage.ProcessingStartedAt = null;

                    _logger.LogWarning(
                        ex,
                        "Falha ao publicar mensagem {MessageId} ({EventType}). Tentativa {Attempt}/{MaxRetries}. Devolvida para Pending.",
                        outboxMessage.Id, outboxMessage.EventType, outboxMessage.RetryCount, MaxRetryAttempts);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishMessageAsync(
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        if (outboxMessage.EventType == "TransactionCreated")
        {
            var message = JsonSerializer.Deserialize<TransactionCreatedMessage>(outboxMessage.Payload)
                ?? throw new InvalidOperationException(
                    $"Falha ao desserializar payload da mensagem {outboxMessage.Id}.");

            await _publishEndpoint.Publish(message, cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Tipo de evento desconhecido '{EventType}' na mensagem {MessageId}. Ignorando.",
                outboxMessage.EventType, outboxMessage.Id);
        }
    }
}
