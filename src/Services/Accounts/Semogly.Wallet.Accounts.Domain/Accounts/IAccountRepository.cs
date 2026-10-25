namespace Semogly.Wallet.Accounts.Domain.Accounts;

/// <summary>Repositório do agregado <see cref="Account"/>, usado pelo lado de escrita.</summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByDocumentAsync(string document, CancellationToken cancellationToken);

    void Add(Account account);
}
