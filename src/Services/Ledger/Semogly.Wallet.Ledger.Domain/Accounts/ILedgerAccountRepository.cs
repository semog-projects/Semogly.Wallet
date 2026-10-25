namespace Semogly.Wallet.Ledger.Domain.Accounts;

/// <summary>Repositório event-sourced: carrega a conta pelos eventos e grava os eventos pendentes.</summary>
public interface ILedgerAccountRepository
{
    Task<LedgerAccount?> GetAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Agenda a gravação dos eventos pendentes, com concorrência otimista pela versão do stream.</summary>
    Task SaveAsync(LedgerAccount account, CancellationToken cancellationToken);
}
