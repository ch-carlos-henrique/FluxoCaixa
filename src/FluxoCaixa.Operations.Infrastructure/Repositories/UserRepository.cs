using FluxoCaixa.Operations.Application.Abstractions;
using FluxoCaixa.Operations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Operations.Infrastructure.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly TransactionDbContext _context;

    public UserRepository(TransactionDbContext context) => _context = context;

    public async Task<UserAuthInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        return user is null ? null : ToAuthInfo(user);
    }

    public async Task<UserAuthInfo?> FindByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.RefreshToken == refreshToken &&
                u.RefreshTokenExpiresAt > DateTime.UtcNow,
            cancellationToken);

        return user is null ? null : ToAuthInfo(user);
    }

    public async Task UpdateRefreshTokenAsync(
        Guid userId,
        string? refreshToken,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FindAsync(new object[] { userId }, cancellationToken);

        if (user is null)
        {
            return;
        }

        user.RefreshToken          = refreshToken;
        user.RefreshTokenExpiresAt = expiresAt;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static UserAuthInfo ToAuthInfo(User user) => new(
        user.Id,
        user.Email,
        user.PasswordHash,
        user.Role,
        user.MerchantId,
        user.RefreshToken,
        user.RefreshTokenExpiresAt);
}
