using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Semogly.Wallet.Gateway.Security;

/// <summary>
/// Emissor de JWT apenas para desenvolvimento. Em produção, os tokens viriam de um
/// Identity Provider (Keycloak, Entra ID etc.) e o Gateway só os validaria.
/// </summary>
internal sealed class TokenIssuer(JwtOptions jwt, TimeProvider clock)
{
    public const string RoleClaim = "role";

    public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt, ClaimsIdentity Identity);

    public IssuedToken Issue(string username, IEnumerable<string>? roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, username) };
        claims.AddRange((roles ?? []).Select(role => new Claim(RoleClaim, role)));

        // Login por e-mail (app do cliente): a claim identifica o titular das contas nos serviços
        if (username.Contains('@', StringComparison.Ordinal))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, username.Trim().ToLowerInvariant()));
        }

        var now = clock.GetUtcNow();
        var expiresAt = now.Add(jwt.TokenLifetime);
        var identity = new ClaimsIdentity(claims, "jwt", JwtRegisteredClaimNames.Sub, RoleClaim);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = identity,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.GetSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return new IssuedToken(token, expiresAt, identity);
    }
}
