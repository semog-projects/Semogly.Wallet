using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Semogly.Wallet.Gateway.Realtime;
using Semogly.Wallet.Gateway.Security;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;

namespace Semogly.Wallet.Gateway.Bff;

/// <summary>
/// Backend for Frontend: o Gateway faz o login OIDC no Keycloak como cliente confidencial e o web fica só
/// com um cookie de sessão. Os tokens ficam no servidor (Redis) e o navegador nunca tem acesso a eles.
/// </summary>
internal static class BffExtensions
{
    private static readonly string[] SafeMethods = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Options];

    public static AuthenticationBuilder AddBffCookie(this AuthenticationBuilder builder) =>
        builder.AddCookie(BffDefaults.CookieScheme, options =>
        {
            options.Cookie.Name = BffDefaults.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;

            // Teto da sessão (o mesmo do SSO do Keycloak); antes disso, ela termina quando o refresh token
            // deixa de valer, por inatividade ou logout no Keycloak
            options.ExpireTimeSpan = TimeSpan.FromHours(10);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = BffSession.RefreshIfExpiringAsync;

            // API, não página: 401/403 em vez de redirecionar para uma tela de login
            options.Events.OnRedirectToLogin = context => WriteStatus(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => WriteStatus(context, StatusCodes.Status403Forbidden);
        });

    /// <summary>
    /// Authorization Code + PKCE com o Keycloak. O retorno chega por GET (response_mode=query), então os
    /// cookies de correlação podem ser SameSite=Lax. A sessão guarda o access token, o mesmo que os serviços validam.
    /// </summary>
    public static AuthenticationBuilder AddBffOpenIdConnect(
        this AuthenticationBuilder builder,
        IHostEnvironment environment,
        KeycloakClientOptions client) =>
        builder.AddKeycloakOpenIdConnect(
            JwtValidationExtensions.KeycloakResource,
            JwtValidationExtensions.Realm,
            BffDefaults.OidcScheme,
            options =>
            {
                options.ClientId = client.ClientId;
                options.ClientSecret = client.ClientSecret;
                options.SignInScheme = BffDefaults.CookieScheme;
                options.CallbackPath = BffDefaults.CallbackPath;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.SaveTokens = false;

                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

                options.Events.OnTokenValidated = context =>
                {
                    var response = context.TokenEndpointResponse!;
                    var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
                    var identity = KeycloakTokenClient.IdentityFrom(response.AccessToken);

                    context.Principal = new ClaimsPrincipal(identity);
                    BffSession.StoreTokens(context.Properties!, new KeycloakTokenClient.IssuedToken(
                        response.AccessToken,
                        response.RefreshToken,
                        clock.GetUtcNow().AddSeconds(int.Parse(response.ExpiresIn, CultureInfo.InvariantCulture)),
                        identity));

                    return Task.CompletedTask;
                };
            });

    /// <summary>Chaves do cookie compartilhadas no Redis: qualquer instância do Gateway abre a sessão.</summary>
    public static IHostApplicationBuilder AddBffDataProtection(this IHostApplicationBuilder builder)
    {
        var redis = builder.Configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("Connection string 'redis' não configurada.");

        builder.Services.AddDataProtection()
            .SetApplicationName("semogly-gateway")
            .PersistKeysToStackExchangeRedis(ConnectionMultiplexer.Connect(redis), "gateway:data-protection-keys");

        return builder;
    }

    /// <summary>Nas chamadas autenticadas por cookie, o YARP envia o JWT da sessão ao serviço.</summary>
    public static IReverseProxyBuilder AddBffTokenForwarding(this IReverseProxyBuilder proxy) =>
        proxy.AddTransforms(transforms => transforms.AddRequestTransform(ForwardSessionTokenAsync));

    internal static async ValueTask ForwardSessionTokenAsync(RequestTransformContext context)
    {
        if (context.HttpContext.Request.Headers.Authorization.Count > 0)
        {
            return;
        }

        var token = await context.HttpContext.GetTokenAsync(BffDefaults.CookieScheme, BffDefaults.AccessTokenName);

        if (token is not null)
        {
            context.ProxyRequest.Headers.Authorization = new("Bearer", token);

            // O serviço não precisa (nem deve receber) o cookie de sessão do navegador
            context.ProxyRequest.Headers.Remove("Cookie");
        }
    }

    /// <summary>Recusa requisições que alteram estado por cookie sem o header anti-CSRF.</summary>
    public static IApplicationBuilder UseBffCsrfProtection(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (RequiresCsrfHeader(context.Request) && !context.Request.Headers.ContainsKey(BffDefaults.CsrfHeader))
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Requisição sem proteção CSRF",
                        detail: $"Requisições autenticadas por cookie que alteram estado exigem o header {BffDefaults.CsrfHeader}.")
                    .ExecuteAsync(context);
                return;
            }

            await next();
        });

    private static bool RequiresCsrfHeader(HttpRequest request)
    {
        if (SafeMethods.Contains(request.Method))
        {
            return false;
        }

        // Bearer não é enviado automaticamente pelo navegador, então não sofre CSRF.
        // O hub só assina grupos de leitura e o cliente SignalR não envia headers no WebSocket.
        return request.Headers.Authorization.Count == 0
               && request.Cookies.ContainsKey(BffDefaults.CookieName)
               && !request.Path.StartsWithSegments(TransfersHub.Route);
    }

    public sealed record BffUser(string Username, IReadOnlyList<string> Roles);

    private static readonly string[] AppRoles = [GatewayPolicies.CustomerRole, GatewayPolicies.OperatorRole];

    /// <summary>
    /// Login e logout do web. O login é uma navegação (não fetch): redireciona para a tela do Keycloak e,
    /// no retorno, cria a sessão e volta para <c>returnUrl</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff").WithTags("BFF");

        group.MapGet("/login", (string? returnUrl) => Results.Challenge(
                new AuthenticationProperties { RedirectUri = IsLocalUrl(returnUrl) ? returnUrl : "/" },
                [BffDefaults.OidcScheme]))
            .AllowAnonymous()
            .WithSummary("Inicia o login OIDC no Keycloak (redireciona); returnUrl precisa ser um caminho local");

        group.MapPost("/logout", async (HttpContext http, KeycloakTokenClient keycloak, CancellationToken cancellationToken) =>
            {
                // Logout federado pelo canal de servidor: encerra a sessão no Keycloak, não só o cookie
                var refreshToken = await http.GetTokenAsync(BffDefaults.CookieScheme, BffDefaults.RefreshTokenName);
                if (refreshToken is not null)
                {
                    await keycloak.LogoutAsync(refreshToken, cancellationToken);
                }

                await http.SignOutAsync(BffDefaults.CookieScheme);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithSummary("Encerra a sessão do web e a do Keycloak. Exige o header X-CSRF");

        group.MapGet("/user", (ClaimsPrincipal user) => Results.Ok(ToUser(user.Identities.First())))
            .RequireAuthorization()
            .WithSummary("Usuário da sessão atual (401 se não houver sessão)");

        return app;
    }

    // Só os papéis da aplicação (o Keycloak também inclui papéis padrão do realm)
    private static BffUser ToUser(ClaimsIdentity identity) => new(
        identity.Name ?? string.Empty,
        identity.FindAll(identity.RoleClaimType).Select(c => c.Value).Where(AppRoles.Contains).ToList());

    // Evita open redirect: só caminhos da própria origem ("/x", nunca "//host" ou "/\host")
    internal static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));

    private static Task WriteStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
