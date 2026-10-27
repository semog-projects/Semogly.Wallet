using Microsoft.AspNetCore.Authentication;
using Semogly.Wallet.Gateway.Bff;

namespace Semogly.Wallet.Gateway.Security;

/// <summary>Token do usuário da requisição, para repassar aos serviços em chamadas feitas pelo próprio Gateway.</summary>
internal static class GatewayAccessToken
{
    public static async Task<string?> FromAsync(HttpContext context) =>
        context.BearerToken()
            // Cliente SignalR (app): token na query string, só aceito na rota do hub
            ?? (string?)context.Request.Query[SecurityExtensions.AccessTokenQueryParameter]
            // Web: token da sessão do BFF
            ?? await context.GetTokenAsync(BffDefaults.CookieScheme, BffDefaults.AccessTokenName);
}
