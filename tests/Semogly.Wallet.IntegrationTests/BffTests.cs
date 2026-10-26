using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Gateway.Bff;
using Semogly.Wallet.IntegrationTests.Infrastructure;
using Yarp.ReverseProxy.Transforms;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>Sessão do web no Gateway: cookie HttpOnly, CSRF e repasse do JWT aos serviços.</summary>
[Collection(WalletTestGroup.Name)]
public sealed class BffTests(WalletFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // HTTPS para o cookie __Host- (Secure) ser aceito e reenviado pelo cliente de teste
    private HttpClient Browser() => fixture.Gateway.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
    });

    [Fact]
    public async Task Login_ShouldCreateHttpOnlySessionWithoutExposingTheToken()
    {
        var browser = Browser();

        var response = await LoginAsync(browser, "ana", withCsrfHeader: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{BffDefaults.CookieName}=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        // A resposta traz o usuário, nunca o JWT
        Assert.DoesNotContain("eyJ", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var user = await browser.GetFromJsonAsync<BffExtensions.BffUser>("/bff/user", Ct);
        Assert.Equal("ana", user!.Username);
    }

    [Fact]
    public async Task Login_WithoutCsrfHeader_ShouldBeRejected()
    {
        var response = await LoginAsync(Browser(), "ana", withCsrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CookieSession_StateChangingRequestWithoutCsrfHeader_ShouldBeRejected()
    {
        var browser = Browser();
        await LoginAsync(browser, "ana", withCsrfHeader: true);

        // Simula um formulário de outro site enviando o cookie automaticamente
        var forged = await browser.PostAsync("/bff/logout", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/bff/user", Ct)).StatusCode);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/bff/logout");
        logout.Headers.Add(BffDefaults.CsrfHeader, "1");
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(logout, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/bff/user", Ct)).StatusCode);
    }

    [Fact]
    public async Task Session_ShouldKeepTheOperatorRole()
    {
        var browser = Browser();
        await LoginAsync(browser, "backoffice", withCsrfHeader: true, roles: ["operator"]);

        var user = await browser.GetFromJsonAsync<BffExtensions.BffUser>("/bff/user", Ct);

        Assert.Equal(["operator"], user!.Roles);
    }

    [Fact]
    public async Task BearerClients_ShouldKeepWorkingWithoutCookie()
    {
        var api = fixture.Gateway.CreateClient();
        var token = await api.PostAsJsonAsync("/auth/token", new { username = "app-mobile" }, Ct);
        var accessToken = (await token.Content.ReadFromJsonAsync<TokenBody>(Ct))!.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var user = await (await api.SendAsync(request, Ct)).Content.ReadFromJsonAsync<BffExtensions.BffUser>(Ct);

        Assert.Equal("app-mobile", user!.Username);
    }

    [Fact]
    public async Task ProxiedRequest_FromCookieSession_ShouldCarryTheSessionTokenAndNotTheCookie()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(new SessionWithToken("token-da-sessao"))
                .BuildServiceProvider(),
        };
        using var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "https://accounts-api/accounts");
        proxyRequest.Headers.Add("Cookie", $"{BffDefaults.CookieName}=valor");

        await BffExtensions.ForwardSessionTokenAsync(new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest,
        });

        Assert.Equal("Bearer", proxyRequest.Headers.Authorization?.Scheme);
        Assert.Equal("token-da-sessao", proxyRequest.Headers.Authorization?.Parameter);
        Assert.False(proxyRequest.Headers.Contains("Cookie"));
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient browser, string username, bool withCsrfHeader, string[]? roles = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BffDefaults.LoginPath)
        {
            Content = JsonContent.Create(new { username, roles }),
        };

        if (withCsrfHeader)
        {
            request.Headers.Add(BffDefaults.CsrfHeader, "1");
        }

        return await browser.SendAsync(request, Ct);
    }

    private sealed record TokenBody(string AccessToken);

    /// <summary>Sessão de cookie autenticada com o JWT guardado nas propriedades.</summary>
    private sealed class SessionWithToken(string token) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = BffDefaults.AccessTokenName, Value = token }]);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("bff")), properties, BffDefaults.CookieScheme)));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
