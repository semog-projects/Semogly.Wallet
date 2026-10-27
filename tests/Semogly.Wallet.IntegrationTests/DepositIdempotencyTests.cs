using System.Net;
using System.Net.Http.Json;
using Semogly.Wallet.IntegrationTests.Infrastructure;
using Semogly.Wallet.Ledger.Application.Accounts.Deposit;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>Depósito com Idempotency-Key: a mesma chave nunca gera dois lançamentos, nem com pedidos simultâneos.</summary>
[Collection(WalletTestGroup.Name)]
public sealed class DepositIdempotencyTests(WalletFixture fixture)
{
    private readonly WalletApi _api = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deposit_RepeatedWithSameKey_ShouldReturnTheOriginalEntry()
    {
        var accountId = await _api.OpenAccountAsync("Ana Deposito Repetido");

        var first = await _api.DepositWithKeyAsync(accountId, 100m, "pix-e2e-1");
        var repeated = await _api.DepositWithKeyAsync(accountId, 100m, "pix-e2e-1");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var original = await first.Content.ReadFromJsonAsync<DepositResponse>(Ct);
        Assert.Equal(original, await repeated.Content.ReadFromJsonAsync<DepositResponse>(Ct));
        Assert.Equal(100m, await _api.GetLedgerBalanceAsync(accountId));
    }

    [Fact]
    public async Task Deposit_SameKeyWithAnotherAmount_ShouldBeConflict()
    {
        var accountId = await _api.OpenAccountAsync("Ana Chave Reusada");
        await _api.DepositWithKeyAsync(accountId, 100m, "pix-e2e-2");

        var response = await _api.DepositWithKeyAsync(accountId, 70m, "pix-e2e-2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(100m, await _api.GetLedgerBalanceAsync(accountId));
    }

    [Fact]
    public async Task Deposit_WithoutKey_ShouldBeBadRequest()
    {
        var accountId = await _api.OpenAccountAsync("Ana Sem Chave");

        var response = await _api.DepositWithKeyAsync(accountId, 100m, idempotencyKey: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0m, await _api.GetLedgerBalanceAsync(accountId));
    }

    [Fact]
    public async Task Deposit_ConcurrentRequestsWithSameKey_ShouldPostOnlyOnce()
    {
        var accountId = await _api.OpenAccountAsync("Ana Deposito Simultaneo");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => _api.DepositWithKeyAsync(accountId, 50m, "pix-e2e-3")));

        // Quem perde a corrida recebe 409 (pode repetir) ou o lançamento original; nunca um segundo lançamento
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(50m, await _api.GetLedgerBalanceAsync(accountId));
    }
}
