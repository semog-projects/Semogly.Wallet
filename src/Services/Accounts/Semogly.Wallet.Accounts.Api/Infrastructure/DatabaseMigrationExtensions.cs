using Microsoft.EntityFrameworkCore;

namespace Semogly.Wallet.Accounts.Api.Infrastructure;

internal static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Aplica as migrations pendentes na inicialização. Usado apenas em desenvolvimento:
    /// em produção as migrations devem rodar em uma etapa separada do deploy.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        await db.Database.MigrateAsync();
    }
}
