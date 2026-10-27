using System.Net.Http.Headers;

namespace Semogly.Wallet.IntegrationTests.Infrastructure;

/// <summary>Envia o token Bearer em cada requisição (obtido no Keycloak dos testes).</summary>
internal sealed class BearerTokenHandler(Func<Task<string>> accessToken) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await accessToken());
        return await base.SendAsync(request, cancellationToken);
    }
}
