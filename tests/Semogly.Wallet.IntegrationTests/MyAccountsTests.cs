using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.IntegrationTests.Infrastructure;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>
/// O Accounts valida por conta própria o JWT emitido pelo Gateway (defesa em profundidade)
/// e identifica o titular pela claim de e-mail.
/// </summary>
[Collection(WalletTestGroup.Name)]
public sealed class MyAccountsTests(WalletFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly WalletApi _api = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Me_WithTokenOfTheOwnerEmail_ShouldReturnOnlyTheOwnerAccounts()
    {
        var owner = $"Titular {Guid.NewGuid():N}";
        var accountId = await _api.OpenAccountAsync(owner);
        await _api.OpenAccountAsync($"Outra Pessoa {Guid.NewGuid():N}");

        // Mesmo e-mail gerado pelo WalletApi; maiúsculas no login não importam
        var email = $"{owner.ToLowerInvariant().Replace(' ', '.')}@email.com";
        var response = await GetMeAsync(await TokenForAsync(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponse>>(Json, Ct);
        Assert.Equal(accountId, Assert.Single(accounts!).Id);
    }

    [Fact]
    public async Task Me_WithoutToken_ShouldBeUnauthorized()
    {
        var response = await GetMeAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenSignedByAnotherKey_ShouldBeUnauthorized()
    {
        // Token de outro emissor (assinatura inválida para este ambiente)
        const string forged =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhbmFAZW1haWwuY29tIiwiZW1haWwiOiJhbmFAZW1haWwuY29tIn0.c2lnbmF0dXJl";

        var response = await GetMeAsync(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ForCustomerWithoutAccounts_ShouldReturnEmptyList()
    {
        var response = await GetMeAsync(await TokenForAsync($"sem.conta.{Guid.NewGuid():N}@email.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<AccountResponse>>(Json, Ct))!);
    }

    [Fact]
    public async Task Lookup_ByRecipientEmail_ShouldReturnIdAndMaskedName()
    {
        var recipient = $"Recebedor Lookup{Guid.NewGuid():N}";
        var accountId = await _api.OpenAccountAsync(recipient);
        var email = $"{recipient.ToLowerInvariant().Replace(' ', '.')}@email.com";

        var response = await LookupAsync(email, await TokenForAsync("pagador@email.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(accountId, body.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal("Recebedor L.", body.RootElement.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Lookup_UnknownEmail_ShouldBeNotFound()
    {
        var response = await LookupAsync($"{Guid.NewGuid():N}@email.com", await TokenForAsync("pagador@email.com"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_WithoutToken_ShouldBeUnauthorized()
    {
        var response = await LookupAsync("alguem@email.com", accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> LookupAsync(string email, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/accounts/lookup?email={Uri.EscapeDataString(email)}");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await fixture.Accounts.CreateClient().SendAsync(request, Ct);
    }

    /// <summary>Token real do Keycloak para um titular (o nome de usuário é o e-mail, como no realm).</summary>
    private async Task<string> TokenForAsync(string username)
    {
        await fixture.Keycloak.CreateAsync(username, username, "customer");
        return await fixture.Keycloak.TokenForAsync(username);
    }

    private async Task<HttpResponseMessage> GetMeAsync(string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/accounts/me");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await fixture.Accounts.CreateClient().SendAsync(request, Ct);
    }

}
