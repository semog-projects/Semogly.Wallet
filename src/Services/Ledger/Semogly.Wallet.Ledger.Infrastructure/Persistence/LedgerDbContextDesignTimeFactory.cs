using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Semogly.Wallet.Ledger.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo dotnet-ef para gerar migrations, sem depender
/// das connection strings que o AppHost injeta em tempo de execução.
/// </summary>
internal sealed class LedgerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql("Host=localhost;Database=ledger-db")
            .Options;

        return new LedgerDbContext(options);
    }
}
