namespace Semogly.Wallet.Accounts.Application.Abstractions;

/// <summary>Confirma as alterações do caso de uso (e as mensagens do outbox) em uma única transação.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
