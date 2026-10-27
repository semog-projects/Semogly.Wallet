namespace Semogly.Wallet.Transfers.Infrastructure.Sagas;

/// <summary>Prazos da saga de transferência (seção "TransferSaga").</summary>
public sealed class TransferSagaOptions
{
    public const string SectionName = "TransferSaga";

    /// <summary>Espera pela análise do antifraude; vencido o prazo, a transferência falha sem mover dinheiro.</summary>
    public TimeSpan FraudCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Espera por cada lançamento do Ledger (débito e crédito); vencido o prazo, a saga pede o cancelamento.</summary>
    public TimeSpan LedgerTimeout { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>Prazo do antifraude vencido (mensagem agendada pela própria saga).</summary>
public sealed record FraudCheckTimedOut(Guid TransferId);

/// <summary>Prazo do lançamento no Ledger vencido (mensagem agendada pela própria saga).</summary>
public sealed record LedgerPostingTimedOut(Guid TransferId);
