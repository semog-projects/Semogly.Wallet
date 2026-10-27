using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Contracts.Fraud;
using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Contracts.Transfers;
using Semogly.Wallet.Transfers.Infrastructure.Sagas;

namespace Semogly.Wallet.Transfers.UnitTests.Sagas;

/// <summary>
/// Exercita a saga com o test harness em memória do MassTransit,
/// simulando as respostas do Fraud e do Ledger.
/// </summary>
public sealed class TransferStateMachineTests : IAsyncLifetime
{
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();
    private const decimal Amount = 250m;

    // Prazos padrão: nos cenários abaixo as respostas chegam bem antes dos timeouts
    private readonly ServiceProvider _provider = SagaTestHost.Build(new TransferSagaOptions());

    private readonly Guid _transferId = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ITestHarness Harness => _provider.GetRequiredService<ITestHarness>();

    private ISagaStateMachineTestHarness<TransferStateMachine, TransferState> Saga =>
        Harness.GetSagaStateMachineHarness<TransferStateMachine, TransferState>();

    private TransferStateMachine Machine => Saga.StateMachine;

    public async ValueTask InitializeAsync() => await Harness.Start();

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task TransferRequested_ShouldRequestFraudCheck()
    {
        await StartTransferAsync();

        Assert.True(await Harness.Published.Any<CheckTransferFraud>(m => m.Context.Message.TransferId == _transferId, Ct));
    }

    [Fact]
    public async Task HappyPath_ShouldDebitCreditAndComplete()
    {
        await StartTransferAsync();

        await PublishAndWaitForStateAsync(new TransferFraudCheckApproved(_transferId), Machine.AwaitingDebit);
        Assert.True(await Harness.Published.Any<DebitAccount>(m => m.Context.Message.AccountId == Source, Ct));

        await PublishAndWaitForStateAsync(Debited(Source), Machine.AwaitingCredit);
        Assert.True(await Harness.Published.Any<CreditAccount>(m => m.Context.Message.AccountId == Destination, Ct));

        await Harness.Bus.Publish(Credited(Destination), Ct);

        Assert.True(await Harness.Published.Any<TransferCompleted>(m => m.Context.Message.TransferId == _transferId, Ct));
        Assert.Null(await Saga.NotExists(_transferId));
    }

    [Fact]
    public async Task FraudRejected_ShouldFailWithoutDebiting()
    {
        await StartTransferAsync();

        await Harness.Bus.Publish(new TransferFraudCheckRejected(_transferId, "Valor acima do limite"), Ct);

        Assert.True(await Harness.Published.Any<TransferFailed>(m => m.Context.Message.Reason == "Valor acima do limite", Ct));
        Assert.False(await Harness.Published.Any<DebitAccount>(Ct));
    }

    [Fact]
    public async Task DebitRejected_ShouldFail()
    {
        await StartTransferAsync();
        await PublishAndWaitForStateAsync(new TransferFraudCheckApproved(_transferId), Machine.AwaitingDebit);

        await Harness.Bus.Publish(new AccountDebitRejected(_transferId, Source, "Saldo insuficiente"), Ct);

        Assert.True(await Harness.Published.Any<TransferFailed>(m => m.Context.Message.Reason == "Saldo insuficiente", Ct));
        Assert.False(await Harness.Published.Any<CreditAccount>(Ct));
    }

    [Fact]
    public async Task CreditRejected_ShouldRefundSourceAndFail()
    {
        await StartTransferAsync();
        await PublishAndWaitForStateAsync(new TransferFraudCheckApproved(_transferId), Machine.AwaitingDebit);
        await PublishAndWaitForStateAsync(Debited(Source), Machine.AwaitingCredit);

        await PublishAndWaitForStateAsync(
            new AccountCreditRejected(_transferId, Destination, "Conta de destino bloqueada"),
            Machine.AwaitingRefund);

        // Estorno: crédito de volta na conta de origem
        Assert.True(await Harness.Published.Any<CreditAccount>(m => m.Context.Message.AccountId == Source, Ct));

        await Harness.Bus.Publish(Credited(Source), Ct);

        Assert.True(await Harness.Published.Any<TransferFailed>(
            m => m.Context.Message.Reason == TransferStateMachine.RefundedReasonPrefix + "Conta de destino bloqueada", Ct));
        Assert.False(await Harness.Published.Any<TransferCompleted>(Ct));
    }

    [Fact]
    public async Task LedgerEntryWithoutTransfer_ShouldBeIgnored()
    {
        await Harness.Bus.Publish(new AccountCredited(Guid.NewGuid(), Destination, Amount, null, DateTimeOffset.UtcNow), Ct);

        Assert.True(await Harness.Consumed.Any<AccountCredited>(Ct));
        Assert.False(await Harness.Published.Any<TransferCompleted>(Ct));
        Assert.DoesNotContain(Harness.Consumed.Select<AccountCredited>(Ct), m => m.Exception is not null);
    }

    private AccountDebited Debited(Guid accountId) =>
        new(Guid.NewGuid(), accountId, Amount, _transferId, DateTimeOffset.UtcNow);

    private AccountCredited Credited(Guid accountId) =>
        new(Guid.NewGuid(), accountId, Amount, _transferId, DateTimeOffset.UtcNow);

    private Task StartTransferAsync() =>
        PublishAndWaitForStateAsync(
            new TransferRequested(_transferId, Source, Destination, Amount, DateTimeOffset.UtcNow),
            Machine.AwaitingFraudCheck);

    private async Task PublishAndWaitForStateAsync<TMessage>(TMessage message, State expectedState)
        where TMessage : class
    {
        await Harness.Bus.Publish(message, Ct);

        Assert.NotNull(await Saga.Exists(_transferId, _ => expectedState));
    }
}
