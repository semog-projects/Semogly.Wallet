using Microsoft.AspNetCore.Authentication.JwtBearer;
using Semogly.Wallet.Gateway.Bff;
using Semogly.Wallet.Gateway.Realtime;

namespace Semogly.Wallet.Gateway.Security;

internal static class SecurityExtensions
{
    /// <summary>Escolhe o esquema por requisição: Bearer (app mobile, Scalar) ou cookie (web via BFF).</summary>
    public const string BearerOrCookieScheme = "bearer-or-cookie";

    public const string AccessTokenQueryParameter = "access_token";

    public static IHostApplicationBuilder AddGatewaySecurity(this IHostApplicationBuilder builder)
    {
        var keycloak = builder.Configuration.GetSection(KeycloakClientOptions.SectionName).Get<KeycloakClientOptions>()
            ?? new KeycloakClientOptions();
        builder.Services.AddSingleton(keycloak);

        // Endereço do Keycloak pelo service discovery do Aspire
        builder.Services.AddHttpClient<KeycloakTokenClient>(
            KeycloakTokenClient.HttpClientName,
            client => client.BaseAddress = new Uri($"https+http://{JwtValidationExtensions.KeycloakResource}"));

        builder.Services.AddAuthentication(BearerOrCookieScheme)
            .AddPolicyScheme(BearerOrCookieScheme, "Bearer ou cookie", options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.Count > 0 || IsHubRequestWithToken(context.Request)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : BffDefaults.CookieScheme)
            .AddWalletJwtBearer(builder.Environment, configure: options =>
                // WebSocket/SSE não permitem o header Authorization no navegador: o cliente
                // SignalR envia o token na query string, aceita apenas na rota do hub
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (IsHubRequestWithToken(context.Request))
                        {
                            context.Token = context.Request.Query[AccessTokenQueryParameter];
                        }

                        return Task.CompletedTask;
                    },
                })
            .AddBffCookie()
            .AddBffOpenIdConnect(builder.Environment, keycloak);

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(GatewayPolicies.Customer, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(GatewayPolicies.Operator, policy => policy.RequireRole(GatewayPolicies.OperatorRole));

        return builder;
    }

    private static bool IsHubRequestWithToken(HttpRequest request) =>
        request.Path.StartsWithSegments(TransfersHub.Route) && request.Query.ContainsKey(AccessTokenQueryParameter);

    public sealed record TokenRequest(string Username, string Password);

    public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

    /// <summary>
    /// Token Bearer do Keycloak por usuário e senha, para o Scalar e o curl. Só em Development:
    /// clientes reais usam Authorization Code + PKCE direto com o Keycloak.
    /// </summary>
    public static IEndpointRouteBuilder MapDevelopmentTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", async (
                TokenRequest request,
                KeycloakTokenClient keycloak,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var token = await keycloak.PasswordGrantAsync(request.Username, request.Password, cancellationToken);

                return token is null
                    ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Usuário ou senha inválidos")
                    : Results.Ok(new TokenResponse(
                        token.AccessToken,
                        "Bearer",
                        (int)(token.ExpiresAt - clock.GetUtcNow()).TotalSeconds));
            })
            .AllowAnonymous()
            .WithTags("Auth")
            .WithSummary("Obtém um token do Keycloak com usuário e senha (desenvolvimento)");

        return app;
    }
}
