using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

namespace FluxoCaixa.Operations.API.Extensions;

internal static class AuthenticationExtensions
{
    /// <summary>
    /// Registra autenticação JWT Bearer (HS256) e autorização baseada em roles.
    /// </summary>
    internal static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Segredo JWT nao configurado (Jwt:Secret).");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = configuration["Jwt:Issuer"]   ?? "FluxoCaixa",
                    ValidAudience            = configuration["Jwt:Audience"] ?? "FluxoCaixa",
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew                = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Registra rate limiting fixo por usuário/IP.
    /// Partition key: nome do usuário autenticado ou endereço IP remoto.
    /// Limite: 200 req/min por cliente (anti-abuso individual; NFR de 50 RPS é throughput agregado).
    /// </summary>
    internal static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(opts =>
        {
            opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit        = 200,
                        Window             = TimeSpan.FromMinutes(1),
                    }));

            opts.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync(
                    "Taxa limite excedida. Tente novamente mais tarde.", token);
            };
        });

        return services;
    }
}
