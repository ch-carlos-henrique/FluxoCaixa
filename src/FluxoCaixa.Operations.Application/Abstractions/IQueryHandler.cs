using FluxoCaixa.Operations.Domain.Common;

namespace FluxoCaixa.Operations.Application.Abstractions;

public interface IQueryHandler<TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
