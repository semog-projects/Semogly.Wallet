using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Semogly.Wallet.IntegrationTests.Infrastructure;

/// <summary>
/// Usuários e tokens reais no Keycloak dos testes: cria usuários pela API de administração, obtém
/// tokens com o mesmo cliente do Gateway (password grant habilitado no realm de desenvolvimento) e
/// preenche a tela de login do Keycloak como um navegador.
/// </summary>
public sealed partial class KeycloakTestUsers(Uri baseAddress, string adminUsername, string adminPassword) : IDisposable
{
    public const string Password = "Senha@Teste1";
    public const string GatewayClientId = "wallet-gateway";
    public const string GatewayClientSecret = "dev-gateway-secret-troque-em-producao";
    private const string Realm = "semogly-wallet";

    public Uri BaseAddress => baseAddress;

    private readonly HttpClient _http = new() { BaseAddress = baseAddress };

    /// <summary>Cria o usuário (o e-mail é opcional) com os papéis do realm informados; ignora se já existir.</summary>
    public async Task CreateAsync(string username, string? email, params string[] roles)
    {
        var admin = await AdminTokenAsync();

        using var create = Authorized(HttpMethod.Post, $"/admin/realms/{Realm}/users", admin);
        create.Content = JsonContent.Create(new
        {
            username,
            email,
            emailVerified = email is not null,
            enabled = true,
            firstName = "Teste",
            lastName = "Integracao",
            credentials = new[] { new { type = "password", value = Password, temporary = false } },
        });
        using var created = await _http.SendAsync(create);
        if (created.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return;
        }

        created.EnsureSuccessStatusCode();
        var userId = created.Headers.Location!.Segments[^1];

        foreach (var role in roles)
        {
            using var getRole = Authorized(HttpMethod.Get, $"/admin/realms/{Realm}/roles/{role}", admin);
            var representation = await (await _http.SendAsync(getRole)).Content.ReadFromJsonAsync<object>();

            using var assign = Authorized(HttpMethod.Post, $"/admin/realms/{Realm}/users/{userId}/role-mappings/realm", admin);
            assign.Content = JsonContent.Create(new[] { representation });
            (await _http.SendAsync(assign)).EnsureSuccessStatusCode();
        }
    }

    /// <summary>Cadastra mais um endereço de retorno no cliente do Gateway (o do servidor de teste).</summary>
    public async Task AllowGatewayRedirectUriAsync(string redirectUri)
    {
        var admin = await AdminTokenAsync();
        var client = await GatewayClientAsync(admin);

        client["redirectUris"]!.AsArray().Add(redirectUri);

        using var update = Authorized(HttpMethod.Put, $"/admin/realms/{Realm}/clients/{client["id"]}", admin);
        update.Content = JsonContent.Create(client);
        (await _http.SendAsync(update)).EnsureSuccessStatusCode();
    }

    /// <summary>Sessões ativas do usuário no Keycloak (para verificar o logout federado).</summary>
    public async Task<int> SessionCountAsync(string username)
    {
        var admin = await AdminTokenAsync();

        using var find = Authorized(HttpMethod.Get, $"/admin/realms/{Realm}/users?exact=true&username={Uri.EscapeDataString(username)}", admin);
        var user = (await (await _http.SendAsync(find)).Content.ReadFromJsonAsync<JsonArray>())!.Single()!;

        using var sessions = Authorized(HttpMethod.Get, $"/admin/realms/{Realm}/users/{user["id"]}/sessions", admin);
        return (await (await _http.SendAsync(sessions)).Content.ReadFromJsonAsync<JsonArray>())!.Count;
    }

    /// <summary>
    /// Faz o papel do navegador na tela do Keycloak: abre a URL de autorização, envia usuário e senha e
    /// devolve o endereço de retorno (redirect_uri com code e state) para onde o Keycloak redireciona.
    /// </summary>
    public static async Task<Uri> SignInAtLoginPageAsync(Uri authorizeUrl, string username)
    {
        // Cookies e redirecionamentos conduzidos à mão: o Keycloak marca os cookies de login como Secure
        // (o CookieContainer não os reenviaria pelo http do container) e o destino final, o Gateway em
        // memória, não é alcançável por um HttpClient comum
        var cookies = new Dictionary<string, string>();
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });

        async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri url, HttpContent? content = null)
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };
            if (cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")));
            }

            var response = await browser.SendAsync(request);
            foreach (var setCookie in response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [])
            {
                var pair = setCookie.Split(';')[0].Split('=', 2);
                cookies[pair[0]] = pair[1];
            }

            return response;
        }

        var page = await SendAsync(HttpMethod.Get, authorizeUrl);
        while (page.StatusCode == HttpStatusCode.Found)
        {
            page = await SendAsync(HttpMethod.Get, new Uri(authorizeUrl, page.Headers.Location!));
        }

        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.IsSuccessStatusCode, $"Tela de login do Keycloak: {(int)page.StatusCode}\n{html}");
        var action = WebUtility.HtmlDecode(LoginFormAction().Match(html).Groups[1].Value);

        using var submitted = await SendAsync(HttpMethod.Post, new Uri(action), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = Password,
            ["credentialId"] = string.Empty,
        }));

        Assert.Equal(HttpStatusCode.Found, submitted.StatusCode);
        return submitted.Headers.Location!;
    }

    /// <summary>Access token do usuário, emitido pelo Keycloak para o cliente do Gateway.</summary>
    public Task<string> TokenForAsync(string username) =>
        TokenAsync(Realm, GatewayClientId, GatewayClientSecret, username, Password);

    private Task<string> AdminTokenAsync() => TokenAsync("master", "admin-cli", null, adminUsername, adminPassword);

    private async Task<JsonObject> GatewayClientAsync(string admin)
    {
        using var find = Authorized(HttpMethod.Get, $"/admin/realms/{Realm}/clients?clientId={GatewayClientId}", admin);
        return (await (await _http.SendAsync(find)).Content.ReadFromJsonAsync<JsonArray>())!.Single()!.AsObject();
    }

    [GeneratedRegex("<form[^>]*id=\"kc-form-login\"[^>]*action=\"([^\"]+)\"")]
    private static partial Regex LoginFormAction();

    private async Task<string> TokenAsync(string realm, string clientId, string? clientSecret, string username, string password)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "openid",
        };
        if (clientSecret is not null)
        {
            form["client_secret"] = clientSecret;
        }

        using var response = await _http.PostAsync(
            $"/realms/{realm}/protocol/openid-connect/token", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public void Dispose() => _http.Dispose();

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}
