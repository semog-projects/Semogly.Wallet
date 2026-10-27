using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Semogly.Wallet.Gateway.Security;

/// <summary>Credenciais do Gateway como cliente confidencial do realm (seção "Keycloak").</summary>
public sealed class KeycloakClientOptions
{
    public const string SectionName = "Keycloak";

    public string ClientId { get; set; } = "wallet-gateway";
    public string ClientSecret { get; set; } = null!;
}

/// <summary>
/// Obtém tokens do Keycloak com usuário e senha (password grant), apenas em desenvolvimento: mantém o Scalar,
/// o curl e os logins de desenvolvimento funcionando. Os clientes finais usam Authorization Code + PKCE.
/// </summary>
internal sealed class KeycloakTokenClient(HttpClient http, KeycloakClientOptions options, TimeProvider clock)
{
    public const string HttpClientName = "keycloak";

    public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt, ClaimsIdentity Identity);

    /// <returns>O token, ou null se usuário ou senha estiverem incorretos.</returns>
    public async Task<IssuedToken?> PasswordGrantAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(
            $"/realms/{JwtValidationExtensions.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid",
            }),
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken))!;

        // Claims só para montar a sessão do BFF: o token acabou de vir do Keycloak pelo canal de servidor
        var jwt = new JsonWebToken(token.AccessToken);
        var identity = new ClaimsIdentity(
            jwt.Claims,
            "keycloak",
            JwtValidationExtensions.NameClaim,
            JwtValidationExtensions.RoleClaim);

        return new IssuedToken(token.AccessToken, clock.GetUtcNow().AddSeconds(token.ExpiresIn), identity);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
