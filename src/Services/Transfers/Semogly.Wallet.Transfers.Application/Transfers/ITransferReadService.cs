using Semogly.Wallet.SharedKernel.Pagination;
using Semogly.Wallet.Transfers.Application.Transfers.ListTransfers;

namespace Semogly.Wallet.Transfers.Application.Transfers;

/// <summary>
/// Lado de leitura (CQRS): consulta projeções prontas para resposta,
/// sem passar pelo modelo de domínio.
/// </summary>
public interface ITransferReadService
{
    Task<TransferResponse?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken);

    Task<PagedResponse<TransferResponse>> ListAsync(TransferFilter filter, CancellationToken cancellationToken);
}
