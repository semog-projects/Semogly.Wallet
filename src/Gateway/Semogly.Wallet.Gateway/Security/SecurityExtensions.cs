using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Semogly.Wallet.Gateway.Bff;
using Semogly.Wallet.Gateway.Realtime;

namespace Semogly.Wallet.Gateway.Security;

internal static class SecurityExtensions
{
    /// <summary>Escolhe o esquema por requisição: Bearer (app mobile, Scalar) ou cookie (web via BFF).</summary>
    public const string BearerOrCookieScheme = "bearer-or-cookie";

    public static IServiceCollection AddGatewaySecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddSingleton(jwt);
        services.AddSingleton<TokenIssuer>();

        services.AddAuthentication(BearerOrCookieScheme)
            .AddPolicyScheme(BearerOrCookieScheme, "Bearer ou cookie", options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.Count > 0 || IsHubRequestWithToken(context.Request)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : BffDefaults.CookieScheme)
            .AddJwtBearer(options =>
            {
                // Mantém os nomes curtos das claims (sub, role) em vez dos URIs do WS-Federation
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.GetSigningKey(),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = TokenIssuer.RoleClaim,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

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
                };
            })
            .AddBffCookie(jwt);

        services.AddAuthorizationBuilder()
            .AddPolicy(GatewayPolicies.Customer, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(GatewayPolicies.Operator, policy => policy.RequireRole(GatewayPolicies.OperatorRole));

        return services;
    }

    private const string AccessTokenQueryParameter = "access_token";

    private static bool IsHubRequestWithToken(HttpRequest request) =>
        request.Path.StartsWithSegments(TransfersHub.Route) && request.Query.ContainsKey(AccessTokenQueryParameter);

    public sealed record TokenRequest(string Username, string[]? Roles);

    public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

    /// <summary>Emissão de token Bearer para clientes que não usam o BFF (app mobile, Scalar). Só em Development.</summary>
    public static IEndpointRouteBuilder MapDevelopmentTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", (TokenRequest request, TokenIssuer issuer, JwtOptions jwt) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.Username)] = ["Informe o usuário."],
                    });
                }

                var token = issuer.Issue(request.Username, request.Roles);
                return Results.Ok(new TokenResponse(token.AccessToken, "Bearer", (int)jwt.TokenLifetime.TotalSeconds));
            })
            .AllowAnonymous()
            .WithTags("Auth")
            .WithSummary("Emite um JWT de desenvolvimento (use roles: [\"operator\"] para registrar depósitos)");

        return app;
    }
}
