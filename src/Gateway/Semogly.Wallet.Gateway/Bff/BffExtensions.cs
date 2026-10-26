using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Semogly.Wallet.Gateway.Realtime;
using Semogly.Wallet.Gateway.Security;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;

namespace Semogly.Wallet.Gateway.Bff;

/// <summary>
/// Backend for Frontend: o web autentica com um cookie de sessão e o JWT fica guardado no servidor,
/// dentro do cookie criptografado. O navegador nunca tem acesso ao token.
/// </summary>
internal static class BffExtensions
{
    private static readonly string[] SafeMethods = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Options];

    public static AuthenticationBuilder AddBffCookie(this AuthenticationBuilder builder, JwtOptions jwt) =>
        builder.AddCookie(BffDefaults.CookieScheme, options =>
        {
            options.Cookie.Name = BffDefaults.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;

            // A sessão não dura mais que o token guardado nela
            options.ExpireTimeSpan = jwt.TokenLifetime;
            options.SlidingExpiration = false;

            // API, não página: 401/403 em vez de redirecionar para uma tela de login
            options.Events.OnRedirectToLogin = context => WriteStatus(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => WriteStatus(context, StatusCodes.Status403Forbidden);
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

        // O login cria a sessão: também protegido contra "login CSRF"
        if (request.Path.Equals(BffDefaults.LoginPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Bearer não é enviado automaticamente pelo navegador, então não sofre CSRF.
        // O hub só assina grupos de leitura e o cliente SignalR não envia headers no WebSocket.
        return request.Headers.Authorization.Count == 0
               && request.Cookies.ContainsKey(BffDefaults.CookieName)
               && !request.Path.StartsWithSegments(TransfersHub.Route);
    }

    public sealed record BffUser(string Username, IReadOnlyList<string> Roles);

    /// <summary>Login e logout do web. O login usa o emissor de desenvolvimento; em produção seria um fluxo OIDC.</summary>
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app, bool allowDevelopmentLogin)
    {
        var group = app.MapGroup("/bff").WithTags("BFF");

        if (allowDevelopmentLogin)
        {
            group.MapPost("/login", async (SecurityExtensions.TokenRequest request, TokenIssuer issuer, HttpContext http) =>
                {
                    if (string.IsNullOrWhiteSpace(request.Username))
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            [nameof(request.Username)] = ["Informe o usuário."],
                        });
                    }

                    var token = issuer.Issue(request.Username, request.Roles);
                    var properties = new AuthenticationProperties { ExpiresUtc = token.ExpiresAt, IsPersistent = true };
                    properties.StoreTokens([new AuthenticationToken { Name = BffDefaults.AccessTokenName, Value = token.AccessToken }]);

                    await http.SignInAsync(BffDefaults.CookieScheme, new ClaimsPrincipal(token.Identity), properties);

                    return Results.Ok(ToUser(token.Identity));
                })
                .AllowAnonymous()
                .WithSummary("Inicia a sessão do web em cookie (desenvolvimento). Exige o header X-CSRF");
        }

        group.MapPost("/logout", async (HttpContext http) =>
            {
                await http.SignOutAsync(BffDefaults.CookieScheme);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithSummary("Encerra a sessão do web. Exige o header X-CSRF");

        group.MapGet("/user", (ClaimsPrincipal user) => Results.Ok(ToUser(user.Identities.First())))
            .RequireAuthorization()
            .WithSummary("Usuário da sessão atual (401 se não houver sessão)");

        return app;
    }

    private static BffUser ToUser(ClaimsIdentity identity) => new(
        identity.Name ?? string.Empty,
        identity.FindAll(identity.RoleClaimType).Select(c => c.Value).ToList());

    private static Task WriteStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
