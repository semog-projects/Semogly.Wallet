using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Validação, nos serviços, do JWT que o Gateway emite e repassa (defesa em profundidade: o serviço não
/// confia apenas no Gateway). Mesmos emissor, audiência e chave configurados no Gateway (seção "Jwt").
/// </summary>
public static class JwtValidationExtensions
{
    public const string EmailClaim = JwtRegisteredClaimNames.Email;

    public static IHostApplicationBuilder AddJwtValidation(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var section = builder.Configuration.GetSection("Jwt");
        var signingKey = section["SigningKey"];

        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey deve ter pelo menos 32 caracteres.");
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Nomes curtos das claims (sub, email, role), como emitidos pelo Gateway
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = section["Issuer"] ?? "semogly-wallet-gateway",
                    ValidAudience = section["Audience"] ?? "semogly-wallet-api",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }
}
