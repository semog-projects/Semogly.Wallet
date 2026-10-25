using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Infrastructure.Persistence;

namespace Semogly.Wallet.Ledger.Infrastructure.EventStore;

/// <summary>Cada conta é um stream de eventos no Marten, identificado pelo Id da conta.</summary>
internal sealed class MartenLedgerAccountRepository(LedgerUnitOfWork unitOfWork) : ILedgerAccountRepository
{
    public async Task<LedgerAccount?> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var session = await unitOfWork.GetSessionAsync(cancellationToken);

        // Reconstrói o agregado pelo stream completo; com streams longos, um snapshot evitaria o replay
        var events = await session.Events.FetchStreamAsync(accountId, token: cancellationToken);

        return events.Count == 0 ? null : LedgerAccount.Rehydrate(events.Select(e => e.Data));
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
            session.Events.Append(account.Id, account.PersistedVersion + events.Length, events);
        }
    }
}
