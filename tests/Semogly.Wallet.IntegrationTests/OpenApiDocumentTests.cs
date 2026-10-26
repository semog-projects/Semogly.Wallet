using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Semogly.Wallet.IntegrationTests.Infrastructure;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>
/// Os documentos OpenAPI alimentam o Scalar e a geração dos clientes dos fronts (TypeScript e Dart):
/// se um deles quebrar, os clientes deixam de ser gerados.
/// </summary>
[Collection(WalletTestGroup.Name)]
public sealed class OpenApiDocumentTests(WalletFixture fixture)
{
    public static TheoryData<string> Services => ["accounts", "transfers", "ledger", "fraud", "notifications"];

    [Theory]
    [MemberData(nameof(Services))]
    public async Task Service_ShouldGenerateItsOpenApiDocument(string service)
    {
        var client = Factory(service).CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{service}: {(int)response.StatusCode} {body}");
        using var document = JsonDocument.Parse(body);
        Assert.NotEmpty(document.RootElement.GetProperty("paths").EnumerateObject());
    }

    private IWebApplicationFactory Factory(string service) => service switch
    {
        "accounts" => new Wrapper<Accounts.Api.AccountsApiMarker>(fixture.Accounts),
        "transfers" => new Wrapper<Transfers.Api.TransfersApiMarker>(fixture.Transfers),
        "ledger" => new Wrapper<Ledger.Api.LedgerApiMarker>(fixture.Ledger),
        "fraud" => new Wrapper<Fraud.Api.FraudApiMarker>(fixture.Fraud),
        "notifications" => new Wrapper<Notifications.Api.NotificationsApiMarker>(fixture.Notifications),
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private interface IWebApplicationFactory
    {
        HttpClient CreateClient();
    }

    private sealed class Wrapper<T>(WebApplicationFactory<T> factory) : IWebApplicationFactory
        where T : class
    {
        public HttpClient CreateClient() => factory.CreateClient();
    }
}
