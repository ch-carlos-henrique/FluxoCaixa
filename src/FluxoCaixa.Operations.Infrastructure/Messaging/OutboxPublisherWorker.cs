using FluxoCaixa.Operations.Application.Contracts;
using FluxoCaixa.Operations.Infrastructure.Persistence;
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
/// Resiliência: Polly v8 — retry 3x com backoff exponencial (2s, 4s, 8s).
/// Após 3 falhas consecutivas na publicação, a mensagem é marcada como "Failed" (DLQ interno).
/// </summary>
public sealed class OutboxPublisherWorker : BackgroundService
{
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);

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

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => m.Status == "Pending")
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processando {Count} mensagem(ns) pendente(s) no outbox.", pendingMessages.Count);

        foreach (var outboxMessage in pendingMessages)
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

                _logger.LogInformation(
                    "Mensagem {MessageId} ({EventType}) publicada com sucesso.",
                    outboxMessage.Id, outboxMessage.EventType);
            }
            catch (Exception ex)
            {
                outboxMessage.RetryCount++;

                if (outboxMessage.RetryCount >= MaxRetryAttempts)
                {
                    // Esgotou as tentativas — move para DLQ interno (status "Failed").
                    outboxMessage.Status = "Failed";
                    _logger.LogError(
                        ex,
                        "Mensagem {MessageId} ({EventType}) falhou após {MaxRetries} tentativas. Status: Failed (DLQ).",
                        outboxMessage.Id, outboxMessage.EventType, MaxRetryAttempts);
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Falha ao publicar mensagem {MessageId} ({EventType}). Tentativa {Attempt}/{MaxRetries}.",
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
