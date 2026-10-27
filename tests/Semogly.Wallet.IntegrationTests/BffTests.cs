using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Gateway.Bff;
using Semogly.Wallet.Gateway.Security;
using Semogly.Wallet.IntegrationTests.Infrastructure;
using Yarp.ReverseProxy.Transforms;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>Sessão do web no Gateway: login OIDC, cookie HttpOnly, CSRF, renovação, logout federado e repasse do JWT.</summary>
[Collection(WalletTestGroup.Name)]
public sealed class BffTests(WalletFixture fixture)
{
    private const string CustomerUser = "ana.bff@email.com";
    private const string OperatorUser = "operador.bff@semogly.dev";
    private const string MobileUser = "app.mobile@email.com";
    private const string LogoutUser = "saida.bff@email.com";
    private const string RefreshUser = "renovacao.bff@email.com";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // HTTPS para o cookie __Host- (Secure) ser aceito e reenviado pelo cliente de teste. Sem seguir
    // redirecionamentos: o vaivém com o Keycloak é conduzido passo a passo
    private HttpClient Browser() => fixture.Gateway.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    [Fact]
    public async Task Login_ShouldRedirectToKeycloakWithPushedAuthorizationRequest()
    {
        var response = await Browser().GetAsync("/bff/login?returnUrl=/accounts", Ct);

        // PAR: os parâmetros (redirect_uri, PKCE, state) foram enviados ao Keycloak pelo canal de servidor;
        // pelo navegador passa só a referência a eles
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var authorize = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.StartsWith(fixture.Keycloak.BaseAddress.ToString(), response.Headers.Location.ToString(), StringComparison.Ordinal);
        Assert.Equal(KeycloakTestUsers.GatewayClientId, authorize["client_id"]);
        Assert.StartsWith("urn:ietf:params:oauth:request_uri:", authorize["request_uri"].ToString(), StringComparison.Ordinal);
        Assert.False(authorize.ContainsKey("code_challenge"));
    }

    [Fact]
    public async Task Login_ShouldCreateHttpOnlySessionWithoutExposingTheToken()
    {
        var browser = Browser();

        var callback = await LoginAsync(browser, CustomerUser, returnUrl: "/accounts");

        // De volta à rota pedida, com o cookie de sessão
        Assert.Equal(HttpStatusCode.Found, callback.StatusCode);
        Assert.Equal("/accounts", callback.Headers.Location!.OriginalString);
        var cookie = Assert.Single(
            callback.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith($"{BffDefaults.CookieName}=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        // O cookie leva só a chave da sessão: os tokens ficam no Redis
        Assert.DoesNotContain("eyJ", cookie, StringComparison.Ordinal);

        var user = await browser.GetFromJsonAsync<BffExtensions.BffUser>("/bff/user", Ct);
        Assert.Equal(CustomerUser, user!.Username);
        Assert.Equal(["customer"], user.Roles);
    }

    [Theory]
    [InlineData("https://site-malicioso.com/")]
    [InlineData("//site-malicioso.com/")]
    [InlineData("/\\site-malicioso.com/")]
    public async Task Login_WithExternalReturnUrl_ShouldReturnToTheRoot(string returnUrl)
    {
        var callback = await LoginAsync(Browser(), CustomerUser, returnUrl);

        Assert.Equal("/", callback.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Session_ShouldKeepTheOperatorRole()
    {
        var browser = Browser();
        await LoginAsync(browser, OperatorUser);

        var user = await browser.GetFromJsonAsync<BffExtensions.BffUser>("/bff/user", Ct);

        Assert.Equal(["operator"], user!.Roles);
    }

    [Fact]
    public async Task Logout_ShouldRequireCsrfHeaderAndEndTheKeycloakSession()
    {
        var browser = Browser();
        await LoginAsync(browser, LogoutUser);
        Assert.Equal(1, await fixture.Keycloak.SessionCountAsync(LogoutUser));

        // Simula um formulário de outro site enviando o cookie automaticamente
        var forged = await browser.PostAsync("/bff/logout", null, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/bff/user", Ct)).StatusCode);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/bff/logout");
        logout.Headers.Add(BffDefaults.CsrfHeader, "1");
        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(logout, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/bff/user", Ct)).StatusCode);
        Assert.Equal(0, await fixture.Keycloak.SessionCountAsync(LogoutUser));
    }

    [Fact]
    public async Task Session_WithExpiringAccessToken_ShouldRefreshWithKeycloak()
    {
        await fixture.Keycloak.CreateAsync(RefreshUser, RefreshUser, "customer");
        var keycloak = KeycloakClient();
        var issued = (await keycloak.PasswordGrantAsync(RefreshUser, KeycloakTestUsers.Password, Ct))!;
        var context = ValidationContext(keycloak, issued, expiresAt: DateTimeOffset.UtcNow.AddSeconds(30));

        await BffSession.RefreshIfExpiringAsync(context);

        Assert.True(context.ShouldRenew);
        Assert.Equal(RefreshUser, context.Principal!.Identity!.Name);
        Assert.NotEqual(issued.AccessToken, context.Properties.GetTokenValue(BffDefaults.AccessTokenName));
        var expiresAt = DateTimeOffset.Parse(
            context.Properties.GetTokenValue(BffDefaults.ExpiresAtName)!, CultureInfo.InvariantCulture);
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task Session_WhenKeycloakSessionEnded_ShouldBeRejected()
    {
        await fixture.Keycloak.CreateAsync(RefreshUser, RefreshUser, "customer");
        var keycloak = KeycloakClient();
        var issued = (await keycloak.PasswordGrantAsync(RefreshUser, KeycloakTestUsers.Password, Ct))!;
        await keycloak.LogoutAsync(issued.RefreshToken!, Ct);
        var authentication = new SessionWithToken("expirado");
        var context = ValidationContext(keycloak, issued, expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1), authentication);

        await BffSession.RefreshIfExpiringAsync(context);

        Assert.Null(context.Principal);
        Assert.True(authentication.SignedOut);
    }

    [Fact]
    public async Task BearerClients_ShouldKeepWorkingWithoutCookie()
    {
        var api = fixture.Gateway.CreateClient();
        await fixture.Keycloak.CreateAsync(MobileUser, MobileUser, "customer");
        var token = await api.PostAsJsonAsync(
            "/auth/token", new { username = MobileUser, password = KeycloakTestUsers.Password }, Ct);
        var accessToken = (await token.Content.ReadFromJsonAsync<TokenBody>(Ct))!.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var user = await (await api.SendAsync(request, Ct)).Content.ReadFromJsonAsync<BffExtensions.BffUser>(Ct);

        Assert.Equal(MobileUser, user!.Username);
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

    /// <summary>Login completo como no navegador: Gateway → tela do Keycloak → retorno ao Gateway.</summary>
    private async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string username, string returnUrl = "/")
    {
        await fixture.Keycloak.CreateAsync(username, username, username == OperatorUser ? "operator" : "customer");

        var challenge = await browser.GetAsync($"/bff/login?returnUrl={Uri.EscapeDataString(returnUrl)}", Ct);
        var callback = await KeycloakTestUsers.SignInAtLoginPageAsync(challenge.Headers.Location!, username);

        return await browser.GetAsync(callback.PathAndQuery, Ct);
    }

    private KeycloakTokenClient KeycloakClient() => new(
        new HttpClient { BaseAddress = fixture.Keycloak.BaseAddress },
        new KeycloakClientOptions { ClientSecret = KeycloakTestUsers.GatewayClientSecret },
        TimeProvider.System);

    /// <summary>Contexto que o cookie do BFF valida a cada requisição, com os tokens da sessão.</summary>
    private static CookieValidatePrincipalContext ValidationContext(
        KeycloakTokenClient keycloak,
        KeycloakTokenClient.IssuedToken issued,
        DateTimeOffset expiresAt,
        SessionWithToken? authentication = null)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(TimeProvider.System)
                .AddSingleton(keycloak)
                .AddSingleton<IAuthenticationService>(authentication ?? new SessionWithToken(issued.AccessToken))
                .BuildServiceProvider(),
        };

        var properties = new AuthenticationProperties();
        BffSession.StoreTokens(properties, issued with { ExpiresAt = expiresAt });

        return new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme(BffDefaults.CookieScheme, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(new ClaimsPrincipal(issued.Identity), properties, BffDefaults.CookieScheme));
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

        public bool SignedOut { get; private set; }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
    }
}
