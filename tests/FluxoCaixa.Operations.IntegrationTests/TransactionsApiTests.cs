using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FluxoCaixa.Operations.IntegrationTests;

public sealed class TransactionsApiTests : IClassFixture<OperationsWebApplicationFactory>
{
    private static readonly Guid MerchantId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherMerchantId = Guid.NewGuid();

    private readonly OperationsWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TransactionsApiTests(OperationsWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/transactions
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostTransaction_WithValidToken_Returns201()
    {
        // Arrange
        var token = _factory.GenerateToken("merchant@fluxocaixa.dev", "Merchant", MerchantId);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Remove("Idempotency-Key");
        _client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var body = new
        {
            merchantId  = MerchantId,
            type        = "Credit",
            amount      = 150.00m,
            currency    = "BRL",
            description = "Venda no crédito",
            occurredAt  = DateTime.UtcNow,
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/transactions", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        result.ShouldNotBeNull();
        result!.Id.ShouldNotBe(Guid.Empty);
        result.MerchantId.ShouldBe(MerchantId);
    }

    [Fact]
    public async Task PostTransaction_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var body = new
        {
            merchantId  = MerchantId,
            type        = "Debit",
            amount      = 50.00m,
            currency    = "BRL",
            description = "Compra",
            occurredAt  = DateTime.UtcNow,
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/transactions", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTransaction_WithDuplicateIdempotencyKey_ReturnsSameId()
    {
        // Arrange
        var token = _factory.GenerateToken("merchant@fluxocaixa.dev", "Merchant", MerchantId);
        var client = _factory.CreateClient();
        var idempotencyKey = $"dup-test-{Guid.NewGuid()}";

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var body = new
        {
            merchantId  = MerchantId,
            type        = "Credit",
            amount      = 200.00m,
            currency    = "BRL",
            description = "Pagamento duplicado",
            occurredAt  = DateTime.UtcNow,
        };

        // Act — primeira chamada
        var first = await client.PostAsJsonAsync("/api/transactions", body);
        first.IsSuccessStatusCode.ShouldBeTrue();
        var firstResult = await first.Content.ReadFromJsonAsync<TransactionResponse>();

        // Act — segunda chamada, mesma chave
        var second = await client.PostAsJsonAsync("/api/transactions", body);
        second.IsSuccessStatusCode.ShouldBeTrue();
        var secondResult = await second.Content.ReadFromJsonAsync<TransactionResponse>();

        // Assert — mesmo ID retornado
        secondResult!.Id.ShouldBe(firstResult!.Id);
    }

    [Fact]
    public async Task PostTransaction_WithDifferentMerchantId_Returns403()
    {
        // Arrange — token criado para MerchantId diferente do body
        var token = _factory.GenerateToken("merchant@fluxocaixa.dev", "Merchant", OtherMerchantId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var body = new
        {
            merchantId  = MerchantId,   // merchant do body != merchant do token
            type        = "Debit",
            amount      = 100.00m,
            currency    = "BRL",
            description = "Tentativa indevida",
            occurredAt  = DateTime.UtcNow,
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/transactions", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostTransaction_WithoutIdempotencyKeyHeader_Returns400()
    {
        // Arrange
        var token = _factory.GenerateToken("merchant@fluxocaixa.dev", "Merchant", MerchantId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        // Sem header Idempotency-Key

        var body = new
        {
            merchantId  = MerchantId,
            type        = "Credit",
            amount      = 75.00m,
            currency    = "BRL",
            description = "Sem chave",
            occurredAt  = DateTime.UtcNow,
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/transactions", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/transactions/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransaction_WithValidId_ReturnsOk()
    {
        // Arrange — cria primeiro uma transação
        var token = _factory.GenerateToken("merchant@fluxocaixa.dev", "Merchant", MerchantId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var body = new
        {
            merchantId  = MerchantId,
            type        = "Credit",
            amount      = 300.00m,
            currency    = "BRL",
            description = "Para busca por ID",
            occurredAt  = DateTime.UtcNow,
        };

        var created   = await client.PostAsJsonAsync("/api/transactions", body);
        var createdDto = await created.Content.ReadFromJsonAsync<TransactionResponse>();

        // Act
        var response = await client.GetAsync($"/api/transactions/{createdDto!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        result!.Id.ShouldBe(createdDto.Id);
    }

    // Tipo auxiliar para desserializar a resposta
    private sealed record TransactionResponse(
        Guid     Id,
        Guid     MerchantId,
        string   Type,
        decimal  Amount,
        string   Currency,
        string?  Description,
        DateTime OccurredAt,
        DateTime CreatedAt);
}
