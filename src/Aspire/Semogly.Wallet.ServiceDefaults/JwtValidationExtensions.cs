using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Validação dos tokens emitidos pelo Keycloak (Identity Provider). Emissor e chaves de assinatura
/// vêm do OIDC discovery do realm, com o endereço do Keycloak resolvido pelo service discovery do Aspire.
/// </summary>
public static class JwtValidationExtensions
{
    /// <summary>Nome do recurso do Keycloak no AppHost (e no service discovery).</summary>
    public const string KeycloakResource = "keycloak";

    public const string Realm = "semogly-wallet";

    /// <summary>Audiência incluída nos tokens pelos clientes do realm.</summary>
    public const string Audience = "semogly-wallet-api";

    public const string EmailClaim = "email";
    public const string NameClaim = "preferred_username";

    /// <summary>Papéis do realm achatados no claim "role" por um mapeador do Keycloak.</summary>
    public const string RoleClaim = "role";

    /// <summary>Papel do backoffice: acesso amplo a contas, transferências e livro-razão.</summary>
    public const string OperatorRole = "operator";

    /// <summary>Política dos endpoints exclusivos do backoffice.</summary>
    public const string OperatorPolicy = "operator";

    public static AuthenticationBuilder AddWalletJwtBearer(
        this AuthenticationBuilder authentication,
        IHostEnvironment environment,
        string scheme = JwtBearerDefaults.AuthenticationScheme,
        Action<JwtBearerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return authentication.AddKeycloakJwtBearer(KeycloakResource, Realm, scheme, options =>
        {
            options.Audience = Audience;

            // Keycloak local em HTTP; fora de desenvolvimento a metadata exige HTTPS
            options.RequireHttpsMetadata = !environment.IsDevelopment();

            // Nomes curtos das claims (preferred_username, email, role), como emitidos pelo Keycloak
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = NameClaim;
            options.TokenValidationParameters.RoleClaimType = RoleClaim;

            configure?.Invoke(options);
        });
    }

    /// <summary>Serviços validam por conta própria o token repassado pelo Gateway (defesa em profundidade).</summary>
    public static IHostApplicationBuilder AddJwtValidation(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddWalletJwtBearer(builder.Environment);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(OperatorPolicy, policy => policy.RequireRole(OperatorRole));

        return builder;
    }
}
