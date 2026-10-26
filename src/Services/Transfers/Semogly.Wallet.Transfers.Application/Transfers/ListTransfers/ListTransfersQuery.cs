using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Pagination;
using Semogly.Wallet.SharedKernel.Results;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.Application.Transfers.ListTransfers;

/// <summary>Filtros do backoffice; a conta casa tanto com a origem quanto com o destino.</summary>
public sealed record TransferFilter(
    TransferStatus? Status,
    Guid? AccountId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);

/// <summary>Transferências mais recentes primeiro, para o painel do backoffice.</summary>
public sealed record ListTransfersQuery(TransferFilter Filter) : IQuery<PagedResponse<TransferResponse>>;

internal sealed class ListTransfersQueryHandler(ITransferReadService readService)
    : IQueryHandler<ListTransfersQuery, PagedResponse<TransferResponse>>
{
    public async Task<Result<PagedResponse<TransferResponse>>> HandleAsync(
        ListTransfersQuery query, CancellationToken cancellationToken)
    {
        if (query.Filter is { From: { } from, To: { } to } && from > to)
        {
            return Error.Validation("Transfer.InvalidPeriod", "A data inicial deve ser anterior à data final.");
        }

        return await readService.ListAsync(query.Filter, cancellationToken);
    }
}
