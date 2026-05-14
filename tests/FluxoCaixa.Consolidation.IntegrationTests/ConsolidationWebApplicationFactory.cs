using FluxoCaixa.Consolidation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace FluxoCaixa.Consolidation.IntegrationTests;

public sealed class ConsolidationWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSecret = "integration-test-secret-min32-chars!!";
    private const string Issuer    = "FluxoCaixa.Operations";
    private const string Audience  = "FluxoCaixa";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("fluxocaixa_cons_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());
    }

    public new async Task DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Consolidation", _postgres.GetConnectionString());
        builder.UseSetting("RabbitMQ:Host",     _rabbit.Hostname);
        builder.UseSetting("RabbitMQ:Username", "guest");
        builder.UseSetting("RabbitMQ:Password", "guest");
        builder.UseSetting("RabbitMQ:Port",     _rabbit.GetMappedPublicPort(5672).ToString());
        builder.UseSetting("Jwt:Secret",         JwtSecret);
        builder.UseSetting("Jwt:Issuer",         Issuer);
        builder.UseSetting("Jwt:Audience",       Audience);

        builder.ConfigureServices(services =>
        {
            // Substitui o DbContext para apontar para o container PostgreSQL de teste.
            services.RemoveAll<DbContextOptions<DailyConsolidationDbContext>>();
            services.AddDbContext<DailyConsolidationDbContext>(opts =>
                opts.UseNpgsql(
                    _postgres.GetConnectionString(),
                    npg => npg.MigrationsHistoryTable("__ef_migrations_history_cons", "public")));
        });
    }

    /// <summary>Gera um token JWT válido para os testes.</summary>
    public string GenerateToken(string email, string role, Guid merchantId)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   email),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role,               role),
            new Claim("merchantId",                  merchantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
