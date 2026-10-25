using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Transfers.Application.Transfers.GetTransfer;

public sealed record GetTransferQuery(Guid TransferId) : IQuery<TransferResponse>;

internal sealed class GetTransferQueryHandler(ITransferReadService readService)
    : IQueryHandler<GetTransferQuery, TransferResponse>
{
    public async Task<Result<TransferResponse>> HandleAsync(GetTransferQuery query, CancellationToken cancellationToken)
    {
        var transfer = await readService.GetByIdAsync(query.TransferId, cancellationToken);

        return transfer is null ? TransferErrors.NotFound(query.TransferId) : transfer;
    }
}
