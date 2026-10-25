namespace Semogly.Wallet.Accounts.Application.Abstractions;

public interface IAccountCache
{
    Task InvalidateAsync(Guid accountId, CancellationToken cancellationToken);
}
