using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;
using Semogly.Wallet.Accounts.Api.Infrastructure;

namespace Semogly.Wallet.Accounts.Api.Features.GetAccount;

internal static class GetAccountEndpoint
{
    public static RouteGroupBuilder MapGetAccount(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetAccount")
            .WithSummary("Consulta os dados de uma conta");

        return group;
    }

    private static async Task<Results<Ok<AccountResponse>, NotFound>> HandleAsync(
        Guid id,
        HybridCache cache,
        AccountsDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await cache.GetAccountAsync(db, id, cancellationToken);

        return account is null ? TypedResults.NotFound() : TypedResults.Ok(account);
    }
}
