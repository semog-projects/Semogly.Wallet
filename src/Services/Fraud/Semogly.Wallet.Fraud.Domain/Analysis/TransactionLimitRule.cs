namespace Semogly.Wallet.Fraud.Domain.Analysis;

/// <summary>
/// Regra sem estado: avaliada só com os dados da transferência. As regras que dependem
/// do histórico da conta (limite diário e velocity) ficam no <c>IAccountActivityStore</c>.
/// </summary>
public static class TransactionLimitRule
{
    public static FraudRejection? Evaluate(decimal amount, FraudPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return amount > policy.MaxTransactionAmount ? FraudRejection.TransactionLimitExceeded : null;
    }
}
