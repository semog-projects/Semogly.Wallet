using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;
using Semogly.Wallet.Accounts.Api.Infrastructure;

namespace Semogly.Wallet.Accounts.Api.Features.GetBalance;

public sealed record BalanceResponse(Guid AccountId, decimal Balance, string Currency);

internal static class GetBalanceEndpoint
{
    public static RouteGroupBuilder MapGetBalance(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/balance", HandleAsync)
            .WithName("GetBalance")
            .WithSummary("Consulta o saldo de uma conta");

        return group;
    }

    private static async Task<Results<Ok<BalanceResponse>, NotFound>> HandleAsync(
        Guid id,
        HybridCache cache,
        AccountsDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await cache.GetAccountAsync(db, id, cancellationToken);

        return account is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new BalanceResponse(account.Id, account.Balance, account.Currency));
    }
}
