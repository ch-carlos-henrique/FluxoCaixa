using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FluxoCaixa.Consolidation.IntegrationTests;

public sealed class ConsolidationApiTests : IClassFixture<ConsolidationWebApplicationFactory>
{
    private static readonly Guid MerchantId      = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherMerchantId = Guid.NewGuid();

    private readonly ConsolidationWebApplicationFactory _factory;

    public ConsolidationApiTests(ConsolidationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/consolidation/daily
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDailyBalance_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var date   = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await client.GetAsync(
            $"/api/consolidation/daily?merchantId={MerchantId}&date={date:yyyy-MM-dd}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDailyBalance_WithValidToken_ReturnsOkOrNotFound()
    {
        // Arrange
        var token  = _factory.GenerateToken("merchant@fluxocaixa.dev", "Merchant", MerchantId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await client.GetAsync(
            $"/api/consolidation/daily?merchantId={MerchantId}&date={date:yyyy-MM-dd}");

        // Assert — 200 (se existir saldo) ou 404 (se ainda não houver consolidação)
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDailyBalance_WithDifferentMerchantId_Returns403()
    {
        // Arrange — token criado para OtherMerchantId, mas query usa MerchantId
        var token  = _factory.GenerateToken("merchant@fluxocaixa.dev", "Merchant", OtherMerchantId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await client.GetAsync(
            $"/api/consolidation/daily?merchantId={MerchantId}&date={date:yyyy-MM-dd}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/consolidation/daily/range
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalanceRange_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var from   = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to     = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await client.GetAsync(
            $"/api/consolidation/daily/range?merchantId={MerchantId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBalanceRange_WithAdminToken_ReturnsOk()
    {
        // Arrange
        var token  = _factory.GenerateToken("admin@fluxocaixa.dev", "Admin", MerchantId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to   = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await client.GetAsync(
            $"/api/consolidation/daily/range?merchantId={MerchantId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        // Assert
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}
