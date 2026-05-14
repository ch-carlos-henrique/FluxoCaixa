using BCrypt.Net;
using FluxoCaixa.Operations.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FluxoCaixa.Operations.Infrastructure.Services;

internal sealed class AuthService : IAuthService
{
    private readonly IUserRepository   _userRepository;
    private readonly IConfiguration    _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration  = configuration;
    }

    public async Task<AuthResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.FindByEmailAsync(email, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var refreshToken = GenerateRefreshToken();
        await _userRepository.UpdateRefreshTokenAsync(
            user.Id,
            refreshToken,
            DateTime.UtcNow.AddDays(GetRefreshExpiryDays()),
            cancellationToken);

        return new AuthResult(
            GenerateAccessToken(user),
            refreshToken,
            GetExpiryMinutes() * 60,
            user.MerchantId,
            user.Role);
    }

    public async Task<AuthResult?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.FindByRefreshTokenAsync(refreshToken, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var newRefreshToken = GenerateRefreshToken();
        await _userRepository.UpdateRefreshTokenAsync(
            user.Id,
            newRefreshToken,
            DateTime.UtcNow.AddDays(GetRefreshExpiryDays()),
            cancellationToken);

        return new AuthResult(
            GenerateAccessToken(user),
            newRefreshToken,
            GetExpiryMinutes() * 60,
            user.MerchantId,
            user.Role);
    }

    private string GenerateAccessToken(UserAuthInfo user)
    {
        var secret   = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Segredo JWT não configurado (Jwt:Secret).");
        var issuer   = _configuration["Jwt:Issuer"]   ?? "FluxoCaixa";
        var audience = _configuration["Jwt:Audience"] ?? "FluxoCaixa";

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("merchant_id",                 user.MerchantId.ToString()),
            new Claim(ClaimTypes.Role,               user.Role),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:            issuer,
            audience:          audience,
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(GetExpiryMinutes()),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private int GetExpiryMinutes()      => int.TryParse(_configuration["Jwt:ExpiryMinutes"],        out var v) ? v : 60;
    private int GetRefreshExpiryDays()  => int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var v) ? v : 7;
}
