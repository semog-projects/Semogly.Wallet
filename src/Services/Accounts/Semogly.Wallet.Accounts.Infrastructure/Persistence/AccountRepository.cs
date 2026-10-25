using Microsoft.EntityFrameworkCore;
using Semogly.Wallet.Accounts.Domain.Accounts;

namespace Semogly.Wallet.Accounts.Infrastructure.Persistence;

internal sealed class AccountRepository(AccountsDbContext db) : IAccountRepository
{
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> ExistsByDocumentAsync(string document, CancellationToken cancellationToken) =>
        db.Accounts.AnyAsync(a => a.Document == document, cancellationToken);

    public void Add(Account account) => db.Accounts.Add(account);
}
