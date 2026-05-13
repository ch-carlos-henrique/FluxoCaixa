using FluxoCaixa.Consolidation.Domain.Common;

namespace FluxoCaixa.Consolidation.Application.Abstractions;

public interface IQueryHandler<TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
