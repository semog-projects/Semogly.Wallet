namespace Semogly.Wallet.Gateway.Bff;

internal static class BffDefaults
{
    public const string CookieScheme = "bff";

    /// <summary>Esquema OIDC do login do web: o Gateway é o cliente confidencial do Keycloak.</summary>
    public const string OidcScheme = "bff-oidc";

    /// <summary>Prefixo __Host-: o navegador só aceita o cookie com Secure, Path=/ e sem Domain.</summary>
    public const string CookieName = "__Host-semogly-bff";

    /// <summary>
    /// Header exigido em requisições que alteram estado autenticadas por cookie. Um site de terceiros
    /// não consegue enviar headers customizados sem passar pelo CORS, o que bloqueia CSRF.
    /// </summary>
    public const string CsrfHeader = "X-CSRF";

    public const string AccessTokenName = "access_token";
    public const string RefreshTokenName = "refresh_token";
    public const string ExpiresAtName = "expires_at";

    public const string LoginPath = "/bff/login";

    /// <summary>Retorno do Keycloak sob /bff: o proxy do front (Vite) já encaminha esse prefixo ao Gateway.</summary>
    public const string CallbackPath = "/bff/signin-oidc";
}
