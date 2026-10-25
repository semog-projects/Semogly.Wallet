using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Semogly.Wallet.Ledger.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Aplica as migrations pendentes na inicialização. Usado apenas em desenvolvimento:
    /// em produção as migrations devem rodar em uma etapa separada do deploy.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IHost app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        await db.Database.MigrateAsync();
    }
}
