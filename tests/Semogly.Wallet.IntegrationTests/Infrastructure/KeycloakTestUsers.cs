using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Semogly.Wallet.IntegrationTests.Infrastructure;

/// <summary>
/// Usuários e tokens reais no Keycloak dos testes: cria usuários pela API de administração e obtém
/// tokens com o mesmo cliente do Gateway (password grant habilitado no realm de desenvolvimento).
/// </summary>
public sealed class KeycloakTestUsers(Uri baseAddress, string adminUsername, string adminPassword) : IDisposable
{
    public const string Password = "Senha@Teste1";
    private const string Realm = "semogly-wallet";
    private const string GatewayClientId = "wallet-gateway";
    private const string GatewayClientSecret = "dev-gateway-secret-troque-em-producao";

    private readonly HttpClient _http = new() { BaseAddress = baseAddress };

    /// <summary>Cria o usuário (o e-mail é opcional) com os papéis do realm informados; ignora se já existir.</summary>
    public async Task CreateAsync(string username, string? email, params string[] roles)
    {
        var admin = await TokenAsync("master", "admin-cli", null, adminUsername, adminPassword);

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

    /// <summary>Access token do usuário, emitido pelo Keycloak para o cliente do Gateway.</summary>
    public Task<string> TokenForAsync(string username) =>
        TokenAsync(Realm, GatewayClientId, GatewayClientSecret, username, Password);

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
