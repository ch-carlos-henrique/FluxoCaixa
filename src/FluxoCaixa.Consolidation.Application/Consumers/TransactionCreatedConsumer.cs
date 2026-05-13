using FluxoCaixa.Consolidation.Application.Contracts;
using FluxoCaixa.Consolidation.Domain.Entities;
using FluxoCaixa.Consolidation.Domain.Repositories;
using FluxoCaixa.Consolidation.Domain.ValueObjects;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Consolidation.Application.Consumers;

public sealed class TransactionCreatedConsumer : IConsumer<TransactionCreatedMessage>
{
    private readonly IDailyBalanceRepository _dailyBalanceRepository;
    private readonly IProcessedMessageRepository _processedMessageRepository;
    private readonly ILogger<TransactionCreatedConsumer> _logger;

    public TransactionCreatedConsumer(
        IDailyBalanceRepository dailyBalanceRepository,
        IProcessedMessageRepository processedMessageRepository,
        ILogger<TransactionCreatedConsumer> logger)
    {
        _dailyBalanceRepository = dailyBalanceRepository;
        _processedMessageRepository = processedMessageRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransactionCreatedMessage> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Processando mensagem {MessageId} — transação {TransactionId} do comerciante {MerchantId}.",
            message.MessageId, message.TransactionId, message.MerchantId);

        // Idempotência: verifica se a mensagem já foi processada antes de agir.
        var alreadyProcessed = await _processedMessageRepository.ExistsAsync(
            message.MessageId, context.CancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogWarning(
                "Mensagem {MessageId} já foi processada anteriormente. Ignorando duplicata.",
                message.MessageId);
            return;
        }

        var date = DailyBalanceDate.From(DateOnly.FromDateTime(message.OccurredAt.Date));

        var dailyBalance = await _dailyBalanceRepository.FindByMerchantAndDateAsync(
            message.MerchantId, date, context.CancellationToken);

        if (dailyBalance is null)
        {
            dailyBalance = DailyBalance.CreateForMerchant(message.MerchantId, date);
            await _dailyBalanceRepository.AddAsync(dailyBalance, context.CancellationToken);
        }

        dailyBalance.Apply(message.TransactionType, message.Amount);

        await _dailyBalanceRepository.SaveChangesAsync(context.CancellationToken);

        // Registra a mensagem como processada após persistir com sucesso.
        await _processedMessageRepository.AddAsync(message.MessageId, context.CancellationToken);

        _logger.LogInformation(
            "Saldo diário atualizado para comerciante {MerchantId} na data {Date}. Créditos: {Credits}, Débitos: {Debits}.",
            message.MerchantId, date.Value, dailyBalance.TotalCredits, dailyBalance.TotalDebits);
    }
}
