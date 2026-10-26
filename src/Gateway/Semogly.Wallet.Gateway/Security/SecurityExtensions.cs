using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Semogly.Wallet.Gateway.Realtime;

namespace Semogly.Wallet.Gateway.Security;

internal static class SecurityExtensions
{
    public static IServiceCollection AddGatewaySecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddSingleton(jwt);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // WebSocket/SSE não permitem o header Authorization no navegador: o cliente
                // SignalR envia o token na query string, aceita apenas na rota do hub
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments(TransfersHub.Route) &&
                            context.Request.Query.TryGetValue("access_token", out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(GatewayPolicies.Customer, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(GatewayPolicies.Operator, policy => policy.RequireRole(GatewayPolicies.OperatorRole));

        return services;
    }

    public sealed record TokenRequest(string Username, string[]? Roles);

    public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

    /// <summary>
    /// Emissor de tokens apenas para desenvolvimento. Em produção, o Gateway só valida tokens
    /// emitidos por um Identity Provider (Keycloak, Entra ID etc.).
    /// </summary>
    public static IEndpointRouteBuilder MapDevelopmentTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", (TokenRequest request, JwtOptions jwt, TimeProvider clock) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(request.Username)] = ["Informe o usuário."],
                    });
                }

                var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, request.Username) };
                claims.AddRange((request.Roles ?? []).Select(role => new Claim("role", role)));

                var now = clock.GetUtcNow();
                var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
                {
                    Issuer = jwt.Issuer,
                    Audience = jwt.Audience,
                    Subject = new ClaimsIdentity(claims),
                    IssuedAt = now.UtcDateTime,
                    NotBefore = now.UtcDateTime,
                    Expires = now.Add(jwt.TokenLifetime).UtcDateTime,
                    SigningCredentials = new SigningCredentials(jwt.GetSigningKey(), SecurityAlgorithms.HmacSha256),
                });

                return Results.Ok(new TokenResponse(token, "Bearer", (int)jwt.TokenLifetime.TotalSeconds));
            })
            .AllowAnonymous()
            .WithTags("Auth")
            .WithSummary("Emite um JWT de desenvolvimento (use roles: [\"operator\"] para registrar depósitos)");

        return app;
    }
}
