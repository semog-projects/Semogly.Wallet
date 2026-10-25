using MassTransit;
using Microsoft.Extensions.Caching.Hybrid;
using Semogly.Wallet.Accounts.Api.Domain;
using Semogly.Wallet.Accounts.Api.Infrastructure;
using Semogly.Wallet.Contracts.Ledger;

namespace Semogly.Wallet.Accounts.Api.Features.ApplyLedgerEntries;

// Atualizam a projeção de saldo a partir dos lançamentos do Ledger.
// A idempotência é garantida pelo Inbox do MassTransit (deduplicação por MessageId)
// e o SaveChanges é feito pelo middleware do outbox ao final do consumo.

internal sealed class AccountDebitedConsumer(AccountsDbContext db, HybridCache cache, TimeProvider clock)
    : IConsumer<AccountDebited>
{
    public async Task Consume(ConsumeContext<AccountDebited> context)
    {
        var entry = context.Message;
        var account = await db.FindAccountAsync(entry.AccountId, context.CancellationToken);

        account.ApplyDebit(entry.Amount, clock.GetUtcNow());

        await cache.RemoveAsync(AccountCache.Key(entry.AccountId), context.CancellationToken);
    }
}

internal sealed class AccountCreditedConsumer(AccountsDbContext db, HybridCache cache, TimeProvider clock)
    : IConsumer<AccountCredited>
{
    public async Task Consume(ConsumeContext<AccountCredited> context)
    {
        var entry = context.Message;
        var account = await db.FindAccountAsync(entry.AccountId, context.CancellationToken);

        account.ApplyCredit(entry.Amount, clock.GetUtcNow());

        await cache.RemoveAsync(AccountCache.Key(entry.AccountId), context.CancellationToken);
    }
}

file static class AccountsDbContextExtensions
{
    public static async Task<Account> FindAccountAsync(
        this AccountsDbContext db, Guid accountId, CancellationToken cancellationToken) =>
        await db.Accounts.FindAsync([accountId], cancellationToken)
            ?? throw new InvalidOperationException($"Conta {accountId} não encontrada.");
}
