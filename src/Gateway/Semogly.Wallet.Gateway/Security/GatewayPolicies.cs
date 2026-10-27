namespace Semogly.Wallet.Gateway.Security;

public static class GatewayPolicies
{
    /// <summary>Qualquer usuário autenticado.</summary>
    public const string Customer = "customer";

    /// <summary>Operações internas, como registrar depósitos (entrada de dinheiro externo).</summary>
    public const string Operator = "operator";

    /// <summary>Papel do realm no Keycloak (claim "role").</summary>
    public const string OperatorRole = "operator";

    public const string CustomerRole = "customer";
}
