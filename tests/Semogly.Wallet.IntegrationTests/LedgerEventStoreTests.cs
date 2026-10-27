using Marten;
using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.IntegrationTests.Infrastructure;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.Infrastructure.EventStore;
using Semogly.Wallet.Ledger.Infrastructure.Persistence;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>Event store do Ledger (Marten) contra um Postgres real.</summary>
[Collection(WalletTestGroup.Name)]
public sealed class LedgerEventStoreTests(WalletFixture fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Events_ShouldOnlyBeVisibleAfterTheSharedTransactionCommits()
    {
        var accountId = Guid.NewGuid();

        // Simula uma queda entre a gravação dos eventos e o commit da transação do EF
        await using (var scope = fixture.Ledger.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<LedgerUnitOfWork>();
            var accounts = scope.ServiceProvider.GetRequiredService<ILedgerAccountRepository>();

            await accounts.SaveAsync(LedgerAccount.Open(accountId, LedgerAccountType.Customer, Now), Ct);
            var session = await unitOfWork.GetSessionAsync(Ct);
            await session.SaveChangesAsync(Ct);
        }

        Assert.Null(await LoadAsync(accountId));

        // Caminho normal: o IUnitOfWork confirma eventos e outbox juntos
        await using (var scope = fixture.Ledger.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<ILedgerAccountRepository>();
            await accounts.SaveAsync(LedgerAccount.Open(accountId, LedgerAccountType.Customer, Now), Ct);
            await scope.ServiceProvider.GetRequiredService<LedgerUnitOfWork>().SaveChangesAsync(Ct);
        }

        Assert.NotNull(await LoadAsync(accountId));
    }

    [Fact]
    public async Task ConcurrentWrites_ToTheSameStream_ShouldConflict()
    {
        var accountId = Guid.NewGuid();
        await using (var setup = fixture.Ledger.Services.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<ILedgerAccountRepository>()
                .SaveAsync(LedgerAccount.Open(accountId, LedgerAccountType.Customer, Now), Ct);
            await setup.ServiceProvider.GetRequiredService<LedgerUnitOfWork>().SaveChangesAsync(Ct);
        }

        // Dois processos leem a mesma versão do stream e tentam gravar
        await using var first = fixture.Ledger.Services.CreateAsyncScope();
        await using var second = fixture.Ledger.Services.CreateAsyncScope();
        var firstWrite = await PrepareDepositAsync(first, accountId);
        var secondWrite = await PrepareDepositAsync(second, accountId);

        await firstWrite();
        await Assert.ThrowsAnyAsync<Exception>(secondWrite);

        var account = await LoadAsync(accountId);
        Assert.Equal(10m, account!.Balance);
    }

    [Fact]
    public async Task Snapshot_ShouldBeWrittenAtEachIntervalAndLoadLikeAFullReplay()
    {
        var accountId = Guid.NewGuid();
        var cashIn = Guid.NewGuid();
        await SaveAsync(LedgerAccount.Open(accountId, LedgerAccountType.Customer, Now));

        // Aberta + 30 créditos = versão 31: snapshot gravado ao cruzar a versão 25
        for (var i = 0; i < 30; i++)
        {
            await using var scope = fixture.Ledger.Services.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<ILedgerAccountRepository>();
            var account = (await accounts.GetAsync(accountId, Ct))!;
            var counterpart = await accounts.GetAsync(cashIn, Ct) ?? LedgerAccount.Open(cashIn, LedgerAccountType.System, Now);

            DoubleEntry.Post(counterpart, account, 10m, null, "Depósito", Now);
            await accounts.SaveAsync(counterpart, Ct);
            await accounts.SaveAsync(account, Ct);
            await scope.ServiceProvider.GetRequiredService<LedgerUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using var query = fixture.Ledger.Services.GetRequiredService<IDocumentStore>().QuerySession();
        var snapshot = await query.LoadAsync<LedgerAccountSnapshot>(accountId, Ct);
        var replayed = LedgerAccount.Rehydrate((await query.Events.FetchStreamAsync(accountId, token: Ct)).Select(e => e.Data));
        var loaded = await LoadAsync(accountId);

        Assert.NotNull(snapshot);
        Assert.Equal(MartenLedgerAccountRepository.SnapshotInterval, snapshot.Version);
        Assert.Equal(240m, snapshot.Balance);
        Assert.Equal(300m, replayed.Balance);
        Assert.Equal(replayed.Balance, loaded!.Balance);
        Assert.Equal(replayed.PersistedVersion, loaded.PersistedVersion);
    }

    private async Task SaveAsync(LedgerAccount account)
    {
        await using var scope = fixture.Ledger.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ILedgerAccountRepository>().SaveAsync(account, Ct);
        await scope.ServiceProvider.GetRequiredService<LedgerUnitOfWork>().SaveChangesAsync(Ct);
    }

    private static async Task<Func<Task>> PrepareDepositAsync(AsyncServiceScope scope, Guid accountId)
    {
        var accounts = scope.ServiceProvider.GetRequiredService<ILedgerAccountRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<LedgerUnitOfWork>();

        var account = (await accounts.GetAsync(accountId, Ct))!;
        var counterpart = LedgerAccount.Open(Guid.NewGuid(), LedgerAccountType.System, Now);
        DoubleEntry.Post(counterpart, account, 10m, null, "Depósito", Now);

        return async () =>
        {
            await accounts.SaveAsync(counterpart, Ct);
            await accounts.SaveAsync(account, Ct);
            await unitOfWork.SaveChangesAsync(Ct);
        };
    }

    private async Task<LedgerAccount?> LoadAsync(Guid accountId)
    {
        await using var scope = fixture.Ledger.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILedgerAccountRepository>().GetAsync(accountId, Ct);
    }
}
