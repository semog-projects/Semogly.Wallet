using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Semogly.Wallet.Notifications.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo dotnet-ef para gerar migrations, sem depender
/// das connection strings que o AppHost injeta em tempo de execução.
/// </summary>
internal sealed class NotificationsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql("Host=localhost;Database=notifications-db")
            .Options;

        return new NotificationsDbContext(options);
    }
}
