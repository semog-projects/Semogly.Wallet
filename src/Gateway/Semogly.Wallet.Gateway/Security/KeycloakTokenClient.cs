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
/// Chamadas de servidor do Gateway ao Keycloak: renovação e encerramento da sessão do BFF e, apenas em
/// desenvolvimento, o password grant usado pelo Scalar e pelo curl.
/// </summary>
internal sealed class KeycloakTokenClient(HttpClient http, KeycloakClientOptions options, TimeProvider clock)
{
    public const string HttpClientName = "keycloak";

    private const string RealmPath = $"/realms/{JwtValidationExtensions.Realm}/protocol/openid-connect";

    public sealed record IssuedToken(
        string AccessToken,
        string? RefreshToken,
        DateTimeOffset ExpiresAt,
        ClaimsIdentity Identity);

    /// <returns>O token, ou null se usuário ou senha estiverem incorretos.</returns>
    public Task<IssuedToken?> PasswordGrantAsync(string username, string password, CancellationToken cancellationToken) =>
        RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid",
            },
            cancellationToken);

    /// <returns>Os novos tokens, ou null se a sessão no Keycloak expirou ou foi encerrada.</returns>
    public Task<IssuedToken?> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            },
            cancellationToken);

    /// <summary>Encerra a sessão do usuário no Keycloak (logout pelo canal de servidor).</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(
            $"{RealmPath}/logout",
            new FormUrlEncodedContent(WithClientCredentials(new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
            })),
            cancellationToken);

        // Sessão já expirada ou encerrada no Keycloak: nada a fazer
        if (response.StatusCode is not HttpStatusCode.BadRequest)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Identidade a partir do access token, o mesmo que os serviços validam: nome em preferred_username
    /// e papéis na claim role. O token acabou de vir do Keycloak pelo canal de servidor.
    /// </summary>
    public static ClaimsIdentity IdentityFrom(string accessToken) => new(
        new JsonWebToken(accessToken).Claims,
        "keycloak",
        JwtValidationExtensions.NameClaim,
        JwtValidationExtensions.RoleClaim);

    private async Task<IssuedToken?> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(
            $"{RealmPath}/token", new FormUrlEncodedContent(WithClientCredentials(form)), cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken))!;

        return new IssuedToken(
            token.AccessToken,
            token.RefreshToken,
            clock.GetUtcNow().AddSeconds(token.ExpiresIn),
            IdentityFrom(token.AccessToken));
    }

    private Dictionary<string, string> WithClientCredentials(Dictionary<string, string> form)
    {
        form["client_id"] = options.ClientId;
        form["client_secret"] = options.ClientSecret;
        return form;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
