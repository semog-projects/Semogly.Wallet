using System.Net;
using System.Net.Http.Json;
using Semogly.Wallet.IntegrationTests.Infrastructure;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>
/// Cada serviço valida o JWT e a posse da conta por conta própria (defesa em profundidade): o cliente só
/// acessa as próprias contas, o operador acessa todas e as rotas do backoffice exigem o papel operator.
/// </summary>
[Collection(WalletTestGroup.Name)]
public sealed class AccountOwnershipTests(WalletFixture fixture)
{
    private readonly WalletApi _api = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Rotas por conta em cada serviço ({0} = id da conta).</summary>
    public static TheoryData<string, string> AccountRoutes => new()
    {
        { "accounts", "/accounts/{0}" },
        { "accounts", "/accounts/{0}/balance" },
        { "ledger", "/ledger/accounts/{0}/statement" },
        { "fraud", "/fraud/accounts/{0}/daily-usage" },
        { "notifications", "/notifications/accounts/{0}" },
    };

    [Theory]
    [MemberData(nameof(AccountRoutes))]
    public async Task Owner_ShouldAccessOwnAccount(string service, string route)
    {
        var (owner, accountId) = await CustomerWithAccountAsync("Titular");

        var response = await Client(service, owner).GetAsync(string.Format(null, route, accountId), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AccountRoutes))]
    public async Task Customer_ShouldBeForbiddenFromAnotherCustomersAccount(string service, string route)
    {
        var (_, accountId) = await CustomerWithAccountAsync("Vitima");
        var intruder = await CustomerAsync("Intruso");

        var response = await Client(service, intruder).GetAsync(string.Format(null, route, accountId), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AccountRoutes))]
    public async Task Request_WithoutToken_ShouldBeUnauthorized(string service, string route)
    {
        var response = await Factory(service).GetAsync(string.Format(null, route, Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_FromOwnAccount_ShouldBeAcceptedAndVisibleToPayerAndRecipientOnly()
    {
        var (payer, source) = await CustomerWithAccountAsync("Pagador", initialDeposit: 50m);
        var (recipient, destination) = await CustomerWithAccountAsync("Recebedor");
        var outsider = await CustomerAsync("Terceiro");

        var response = await RequestTransferAsync(payer, source, destination);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var location = response.Headers.Location!;

        Assert.Equal(HttpStatusCode.OK, (await fixture.As(fixture.Transfers, payer).GetAsync(location, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.As(fixture.Transfers, recipient).GetAsync(location, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.As(fixture.Transfers, outsider).GetAsync(location, Ct)).StatusCode);
    }

    [Fact]
    public async Task Transfer_FromAnotherCustomersAccount_ShouldBeForbidden()
    {
        var (_, victimAccount) = await CustomerWithAccountAsync("Vitima Transferencia", initialDeposit: 50m);
        var (intruder, intruderAccount) = await CustomerWithAccountAsync("Intruso Transferencia");

        var response = await RequestTransferAsync(intruder, victimAccount, intruderAccount);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(50m, await _api.GetLedgerBalanceAsync(victimAccount));
    }

    [Fact]
    public async Task OpenAccount_ForAnotherEmail_ShouldBeForbidden_AndForOwnEmailShouldBeCreated()
    {
        var customer = await CustomerAsync("Novo Cliente");
        var client = fixture.As(fixture.Accounts, customer);

        var foreign = await client.PostAsJsonAsync(
            "/accounts", new { ownerName = "Outra Pessoa", document = RandomCpf(), email = "outra.pessoa@email.com" }, Ct);
        var own = await client.PostAsJsonAsync(
            "/accounts", new { ownerName = "Novo Cliente", document = RandomCpf(), email = customer }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
    }

    [Theory]
    [InlineData("accounts", "GET", "/accounts")]
    [InlineData("transfers", "GET", "/transfers")]
    [InlineData("ledger", "GET", "/ledger/book")]
    [InlineData("ledger", "POST", "/ledger/accounts/{0}/deposits")]
    public async Task Customer_ShouldBeForbiddenFromBackofficeRoutes(string service, string method, string route)
    {
        var (customer, accountId) = await CustomerWithAccountAsync("Cliente Backoffice");

        using var request = new HttpRequestMessage(new HttpMethod(method), string.Format(null, route, accountId))
        {
            Content = method == "POST" ? JsonContent.Create(new { amount = 1_000m }) : null,
        };
        var response = await Client(service, customer).SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> RequestTransferAsync(string customer, Guid source, Guid destination)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(new { sourceAccountId = source, destinationAccountId = destination, amount = 10m }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        return await fixture.As(fixture.Transfers, customer).SendAsync(request, Ct);
    }

    /// <summary>Cliente no Keycloak (o nome de usuário é o e-mail, como no realm).</summary>
    private async Task<string> CustomerAsync(string name)
    {
        var email = WalletApi.EmailFor($"{name} {Guid.NewGuid():N}");
        await fixture.Keycloak.CreateAsync(email, email, "customer");
        return email;
    }

    private async Task<(string Customer, Guid AccountId)> CustomerWithAccountAsync(string name, decimal initialDeposit = 0m)
    {
        var customer = await CustomerAsync(name);
        return (customer, await _api.OpenAccountAsync(name, initialDeposit, customer));
    }

    private HttpClient Client(string service, string username) => service switch
    {
        "accounts" => fixture.As(fixture.Accounts, username),
        "transfers" => fixture.As(fixture.Transfers, username),
        "ledger" => fixture.As(fixture.Ledger, username),
        "fraud" => fixture.As(fixture.Fraud, username),
        "notifications" => fixture.As(fixture.Notifications, username),
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private HttpClient Factory(string service) => service switch
    {
        "accounts" => fixture.Accounts.CreateClient(),
        "transfers" => fixture.Transfers.CreateClient(),
        "ledger" => fixture.Ledger.CreateClient(),
        "fraud" => fixture.Fraud.CreateClient(),
        "notifications" => fixture.Notifications.CreateClient(),
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private static string RandomCpf() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString("D11", null);
}
