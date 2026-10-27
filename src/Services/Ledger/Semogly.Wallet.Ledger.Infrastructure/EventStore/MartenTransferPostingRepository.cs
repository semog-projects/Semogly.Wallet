using Semogly.Wallet.Ledger.Domain.Transfers;
using Semogly.Wallet.Ledger.Infrastructure.Persistence;

namespace Semogly.Wallet.Ledger.Infrastructure.EventStore;

/// <summary>Cada transferência é um stream no Marten, identificado pelo Id da transferência.</summary>
internal sealed class MartenTransferPostingRepository(LedgerUnitOfWork unitOfWork) : ITransferPostingRepository
{
    public async Task<TransferPosting> GetAsync(Guid transferId, CancellationToken cancellationToken)
    {
        var session = await unitOfWork.GetSessionAsync(cancellationToken);
        var events = await session.Events.FetchStreamAsync(transferId, token: cancellationToken);

        return TransferPosting.Rehydrate(transferId, events.Select(e => e.Data));
    }

    public async Task SaveAsync(TransferPosting posting, CancellationToken cancellationToken)
    {
        if (posting.PendingEvents.Count == 0)
        {
            return;
        }

        var session = await unitOfWork.GetSessionAsync(cancellationToken);
        var events = posting.PendingEvents.ToArray();

        // Concorrência otimista: débito, crédito e cancelamento simultâneos da mesma transferência não passam
        // juntos; o perdedor recebe conflito e o retry reprocessa com o estado atualizado
        if (posting.PersistedVersion == 0)
        {
            session.Events.StartStream(posting.Id, events);
        }
        else
        {
            session.Events.Append(posting.Id, posting.PersistedVersion + events.Length, events);
        }
    }
}
