using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.SharedKernel.Domain;

namespace Semogly.Wallet.Ledger.UnitTests.Domain;

public class LedgerAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static LedgerAccount Customer() => LedgerAccount.Open(Guid.NewGuid(), LedgerAccountType.Customer, Now);

    private static LedgerAccount System() => LedgerAccount.Open(Guid.NewGuid(), LedgerAccountType.System, Now);

    [Fact]
    public void Post_ShouldMoveAmountAndKeepBookBalanced()
    {
        var cashIn = System();
        var customer = Customer();

        var (debit, credit) = DoubleEntry.Post(cashIn, customer, 150m, null, "Depósito", Now);

        Assert.Equal(-150m, cashIn.Balance);
        Assert.Equal(150m, customer.Balance);
        Assert.Equal(debit.Amount, credit.Amount);
        Assert.Equal(0m, cashIn.Balance + customer.Balance);
    }

    [Fact]
    public void Restore_FromSnapshotAndLaterEvents_ShouldMatchFullReplay()
    {
        var cashIn = System();
        var customer = Customer();
        DoubleEntry.Post(cashIn, customer, 100m, null, "Depósito", Now);
        DoubleEntry.Post(cashIn, customer, 50m, null, "Depósito", Now);
        var events = customer.PendingEvents.ToList(); // aberto + 2 créditos

        // Snapshot na versão 2 (aberto + 1º crédito) e o 2º crédito depois dele
        var restored = LedgerAccount.Restore(customer.Id, LedgerAccountType.Customer, 100m, 2, events.Skip(2));
        var replayed = LedgerAccount.Rehydrate(events);

        Assert.Equal(replayed.Balance, restored.Balance);
        Assert.Equal(150m, restored.Balance);
        Assert.Equal(3, restored.PersistedVersion);
        Assert.Equal(replayed.PersistedVersion, restored.PersistedVersion);
    }

    [Fact]
    public void Version_ShouldCountPendingEvents()
    {
        var customer = Customer();

        Assert.Equal(0, customer.PersistedVersion);
        Assert.Equal(1, customer.Version);
    }

    [Fact]
    public void Post_FromCustomerWithoutFunds_ShouldThrow()
    {
        var customer = Customer();

        Assert.False(customer.CanDebit(10m));
        Assert.Throws<DomainException>(() => DoubleEntry.Post(customer, System(), 10m, null, "Envio", Now));
    }

    [Fact]
    public void Post_ToSameAccount_ShouldThrow()
    {
        var account = System();

        Assert.Throws<DomainException>(() => DoubleEntry.Post(account, account, 10m, null, "Inválido", Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Post_WithNonPositiveAmount_ShouldThrow(decimal amount) =>
        Assert.Throws<DomainException>(() => DoubleEntry.Post(System(), Customer(), amount, null, "Inválido", Now));

    [Fact]
    public void Rehydrate_ShouldRebuildStateAndVersionFromEvents()
    {
        var cashIn = System();
        var customer = Customer();
        DoubleEntry.Post(cashIn, customer, 100m, null, "Depósito", Now);
        DoubleEntry.Post(customer, cashIn, 30m, null, "Saque", Now);

        var rebuilt = LedgerAccount.Rehydrate(customer.PendingEvents);

        Assert.Equal(customer.Id, rebuilt.Id);
        Assert.Equal(70m, rebuilt.Balance);
        Assert.Equal(3, rebuilt.PersistedVersion);
        Assert.Empty(rebuilt.PendingEvents);
    }
}
