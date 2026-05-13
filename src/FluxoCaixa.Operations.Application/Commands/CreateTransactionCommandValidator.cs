using FluentValidation;

namespace FluxoCaixa.Operations.Application.Commands;

public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty().WithMessage("O ID do comerciante é obrigatório.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("O tipo da transação é obrigatório.")
            .Must(t => t == "Credit" || t == "Debit")
            .WithMessage("O tipo da transação deve ser 'Credit' ou 'Debit'.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("O valor deve ser maior que zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("A moeda é obrigatória.")
            .Length(3).WithMessage("A moeda deve ser um código ISO de 3 letras (ex: BRL).");

        RuleFor(x => x.OccurredAt)
            .NotEqual(default(DateTime)).WithMessage("A data de ocorrência é obrigatória.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("A chave de idempotência é obrigatória.")
            .MaximumLength(128).WithMessage("A chave de idempotência não pode exceder 128 caracteres.");
    }
}
