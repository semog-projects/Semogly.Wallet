using Semogly.Wallet.Fraud.Domain.Analysis;

namespace Semogly.Wallet.Fraud.Application.Abstractions;

/// <summary>Histórico recente de transferências por conta, usado pelas regras com estado.</summary>
public interface IAccountActivityStore
{
    /// <summary>
    /// Verifica o limite diário e o velocity e, se a transferência passar, a registra, tudo de forma atômica.
    /// Registrar a mesma transferência de novo não a conta duas vezes.
    /// </summary>
    /// <returns>O motivo da recusa, ou <c>null</c> se aprovada.</returns>
    Task<FraudRejection?> TryRegisterAsync(
        Guid accountId, Guid transferId, decimal amount, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Devolve ao limite diário o valor de uma transferência que não se concretizou.</summary>
    Task ReleaseAsync(Guid accountId, Guid transferId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<decimal> GetDailyUsageAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken);
}
