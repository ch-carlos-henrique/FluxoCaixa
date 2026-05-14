namespace FluxoCaixa.Operations.Infrastructure.Persistence;

/// <summary>
/// Entidade interna que representa um usuário do sistema (Merchant ou Admin).
/// Tabela: users — gerenciada manualmente sem ASP.NET Core Identity.
/// </summary>
internal sealed class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hash da senha gerado com BCrypt.Net-Next (work factor 12, salt embutido).
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Role: "Merchant" ou "Admin".
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public Guid MerchantId { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }
}
