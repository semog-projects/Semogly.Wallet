namespace Semogly.Wallet.Transfers.Application.Transfers;

/// <summary>
/// Lado de leitura (CQRS): consulta projeções prontas para resposta,
/// sem passar pelo modelo de domínio.
/// </summary>
public interface ITransferReadService
{
    Task<TransferResponse?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken);
}
