using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Semogly.Wallet.Accounts.Api.Infrastructure;

/// <summary>
/// Usada apenas pelo dotnet-ef para gerar migrations, sem depender
/// das connection strings que o AppHost injeta em tempo de execução.
/// </summary>
internal sealed class AccountsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AccountsDbContext>
{
    public AccountsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AccountsDbContext>()
            .UseNpgsql("Host=localhost;Database=accounts-db")
            .Options;

        return new AccountsDbContext(options);
    }
}
