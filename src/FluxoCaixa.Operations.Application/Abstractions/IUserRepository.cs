namespace FluxoCaixa.Operations.Application.Abstractions;

public sealed record UserAuthInfo(
    Guid Id,
    string Email,
    string PasswordHash,
    string Role,
    Guid MerchantId,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt);

public interface IUserRepository
{
    Task<UserAuthInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserAuthInfo?> FindByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task UpdateRefreshTokenAsync(Guid userId, string? refreshToken, DateTime? expiresAt, CancellationToken cancellationToken = default);
}
