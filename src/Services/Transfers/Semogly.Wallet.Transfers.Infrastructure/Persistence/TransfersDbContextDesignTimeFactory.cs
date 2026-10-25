using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Semogly.Wallet.Transfers.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo dotnet-ef para gerar migrations, sem depender
/// das connection strings que o AppHost injeta em tempo de execução.
/// </summary>
internal sealed class TransfersDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TransfersDbContext>
{
    public TransfersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseNpgsql("Host=localhost;Database=transfers-db")
            .Options;

        return new TransfersDbContext(options);
    }
}
