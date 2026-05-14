namespace FluxoCaixa.Operations.Application.Abstractions;

public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    Guid MerchantId,
    string Role);

public interface IAuthService
{
    Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
