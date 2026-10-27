using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.IntegrationTests.Infrastructure;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>
/// Cancelamento por timeout da saga no Ledger real (RabbitMQ + Marten): os comandos são publicados direto no
/// barramento, simulando uma saga cujo prazo venceu com débito ou crédito ainda pendente.
/// </summary>
[Collection(WalletTestGroup.Name)]
public sealed class TransferCancellationTests(WalletFixture fixture)
{
    private readonly WalletApi _api = new(fixture);

    private IBus Bus => fixture.Ledger.Services.GetRequiredService<IBus>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cancel_AfterDebit_ShouldRefundSource_AndIgnoreLateCredit()
    {
        var ana = await _api.OpenAccountAsync("Ana Cancelada", initialDeposit: 100m);
        var bruno = await _api.OpenAccountAsync("Bruno Cancelado");
        var transferId = Guid.CreateVersion7();

        await Bus.Publish(new DebitAccount(transferId, ana, 30m), Ct);
        await Eventually.GetAsync(() => _api.GetLedgerBalanceAsync(ana), balance => balance == 70m, "débito lançado");

        await Bus.Publish(new CancelTransferPosting(transferId), Ct);
        await Eventually.GetAsync(() => _api.GetLedgerBalanceAsync(ana), balance => balance == 100m, "estorno após o cancelamento");

        // Crédito atrasado depois do cancelamento: não pode ser lançado (o valor já voltou à origem)
        await Bus.Publish(new CreditAccount(transferId, bruno, 30m), Ct);
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        Assert.Equal(0m, await _api.GetLedgerBalanceAsync(bruno));
        Assert.Equal(100m, await _api.GetLedgerBalanceAsync(ana));
        await Eventually.GetAsync(() => _api.GetBalanceAsync(ana), balance => balance == 100m, "estorno refletido no Accounts");
    }

    [Fact]
    public async Task Cancel_BeforeDebit_ShouldIgnoreLateDebit()
    {
        var ana = await _api.OpenAccountAsync("Ana Debito Tardio", initialDeposit: 100m);
        var transferId = Guid.CreateVersion7();

        await Bus.Publish(new CancelTransferPosting(transferId), Ct);
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        await Bus.Publish(new DebitAccount(transferId, ana, 30m), Ct);
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        Assert.Equal(100m, await _api.GetLedgerBalanceAsync(ana));
    }
}
