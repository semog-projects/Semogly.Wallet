using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Infrastructure.Persistence;

namespace Semogly.Wallet.Ledger.Infrastructure.EventStore;

/// <summary>
/// Cada conta é um stream de eventos no Marten, identificado pelo Id da conta. Um snapshot (documento) guarda o
/// estado a cada <see cref="SnapshotInterval"/> eventos: a carga lê o snapshot e só os eventos posteriores a ele.
/// </summary>
internal sealed class MartenLedgerAccountRepository(LedgerUnitOfWork unitOfWork) : ILedgerAccountRepository
{
    /// <summary>
    /// Eventos entre snapshots. Contas de sistema (CashIn, Clearing) recebem um evento por depósito ou
    /// transferência, então sem snapshot cada operação releria um stream cada vez maior.
    /// </summary>
    public const int SnapshotInterval = 25;

    public async Task<LedgerAccount?> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var session = await unitOfWork.GetSessionAsync(cancellationToken);
        var snapshot = await session.LoadAsync<LedgerAccountSnapshot>(accountId, cancellationToken);

        if (snapshot is null)
        {
            // Conta nova ou anterior aos snapshots: o próximo salvamento que cruzar o intervalo grava um
            var events = await session.Events.FetchStreamAsync(accountId, token: cancellationToken);
            return events.Count == 0 ? null : LedgerAccount.Rehydrate(events.Select(e => e.Data));
        }

        var laterEvents = await session.Events.FetchStreamAsync(
            accountId, fromVersion: snapshot.Version + 1, token: cancellationToken);

        return LedgerAccount.Restore(
            snapshot.Id, snapshot.Type, snapshot.Balance, snapshot.Version, laterEvents.Select(e => e.Data));
    }

    public async Task SaveAsync(LedgerAccount account, CancellationToken cancellationToken)
    {
        if (account.PendingEvents.Count == 0)
        {
            return;
        }

        var session = await unitOfWork.GetSessionAsync(cancellationToken);
        var events = account.PendingEvents.ToArray();

        if (account.PersistedVersion == 0)
        {
            session.Events.StartStream(account.Id, events);
        }
        else
        {
            // Versão esperada após o append: se outro processo gravou antes, o Marten lança conflito e o retry reprocessa
            session.Events.Append(account.Id, account.Version, events);
        }

        // Snapshot ao cruzar um múltiplo do intervalo, na mesma transação dos eventos: nunca fica à frente do stream
        if (account.Version / SnapshotInterval > account.PersistedVersion / SnapshotInterval)
        {
            session.Store(LedgerAccountSnapshot.From(account));
        }
    }
}

/// <summary>Estado da conta em uma versão do stream (documento do Marten).</summary>
public sealed record LedgerAccountSnapshot(Guid Id, LedgerAccountType Type, decimal Balance, long Version)
{
    public static LedgerAccountSnapshot From(LedgerAccount account) =>
        new(account.Id, account.Type, account.Balance, account.Version);
}
