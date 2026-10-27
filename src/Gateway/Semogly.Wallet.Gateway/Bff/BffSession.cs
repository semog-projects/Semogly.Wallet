using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Semogly.Wallet.Gateway.Security;

namespace Semogly.Wallet.Gateway.Bff;

/// <summary>Tokens do Keycloak guardados na sessão do BFF e a renovação deles pelo refresh token.</summary>
internal static class BffSession
{
    /// <summary>Renova um pouco antes de expirar, para o token não vencer no caminho até o serviço.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    public static void StoreTokens(AuthenticationProperties properties, KeycloakTokenClient.IssuedToken token)
    {
        List<AuthenticationToken> tokens =
        [
            new() { Name = BffDefaults.AccessTokenName, Value = token.AccessToken },
            new() { Name = BffDefaults.ExpiresAtName, Value = token.ExpiresAt.ToString("o", CultureInfo.InvariantCulture) },
        ];

        if (token.RefreshToken is not null)
        {
            tokens.Add(new() { Name = BffDefaults.RefreshTokenName, Value = token.RefreshToken });
        }

        properties.StoreTokens(tokens);
    }

    /// <summary>
    /// Executado a cada requisição autenticada por cookie: com o access token perto de expirar, pede novos tokens
    /// ao Keycloak e regrava a sessão. Se a sessão no Keycloak acabou (expirada ou encerrada), encerra a do BFF.
    /// </summary>
    public static async Task RefreshIfExpiringAsync(CookieValidatePrincipalContext context)
    {
        var expiresAt = context.Properties.GetTokenValue(BffDefaults.ExpiresAtName);
        var refreshToken = context.Properties.GetTokenValue(BffDefaults.RefreshTokenName);

        if (expiresAt is null || refreshToken is null)
        {
            return;
        }

        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        if (DateTimeOffset.Parse(expiresAt, CultureInfo.InvariantCulture) - RefreshMargin > now)
        {
            return;
        }

        var token = await services.GetRequiredService<KeycloakTokenClient>()
            .RefreshAsync(refreshToken, context.HttpContext.RequestAborted);

        if (token is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(BffDefaults.CookieScheme);
            return;
        }

        StoreTokens(context.Properties, token);

        // Papéis podem ter mudado no Keycloak desde o login
        context.ReplacePrincipal(new ClaimsPrincipal(token.Identity));
        context.ShouldRenew = true;
    }
}
