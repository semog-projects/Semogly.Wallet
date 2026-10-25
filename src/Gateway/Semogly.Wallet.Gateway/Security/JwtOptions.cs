using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Semogly.Wallet.Gateway.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "semogly-wallet-gateway";
    public string Audience { get; set; } = "semogly-wallet-api";

    /// <summary>Chave HMAC (mínimo de 32 caracteres), injetada pelo AppHost como parâmetro secreto.</summary>
    public string SigningKey { get; set; } = null!;

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);

    public SymmetricSecurityKey GetSigningKey() =>
        string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32
            ? throw new InvalidOperationException("Jwt:SigningKey deve ter pelo menos 32 caracteres.")
            : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
}

public static class GatewayPolicies
{
    /// <summary>Qualquer usuário autenticado.</summary>
    public const string Customer = "customer";

    /// <summary>Operações internas, como registrar depósitos (entrada de dinheiro externo).</summary>
    public const string Operator = "operator";

    public const string OperatorRole = "operator";
}
