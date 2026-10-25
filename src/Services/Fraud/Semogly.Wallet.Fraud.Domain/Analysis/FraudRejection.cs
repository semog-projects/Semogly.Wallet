namespace Semogly.Wallet.Fraud.Domain.Analysis;

public enum FraudRejection
{
    TransactionLimitExceeded,
    DailyLimitExceeded,
    VelocityExceeded,
}

public static class FraudRejectionExtensions
{
    /// <summary>Motivo exibido ao cliente no status da transferência.</summary>
    public static string ToReason(this FraudRejection rejection) => rejection switch
    {
        FraudRejection.TransactionLimitExceeded => "Valor acima do limite por transação.",
        FraudRejection.DailyLimitExceeded => "Limite diário de transferências excedido.",
        FraudRejection.VelocityExceeded => "Muitas transferências em sequência. Tente novamente em instantes.",
        _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, null),
    };
}
