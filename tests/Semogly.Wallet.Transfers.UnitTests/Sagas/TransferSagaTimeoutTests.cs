using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Contracts.Fraud;
using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Contracts.Transfers;
using Semogly.Wallet.Transfers.Infrastructure.Sagas;

namespace Semogly.Wallet.Transfers.UnitTests.Sagas;

/// <summary>Timeouts da saga com prazos curtos: o agendador em memória entrega as mensagens de prazo vencido.</summary>
public sealed class TransferSagaTimeoutTests : IAsyncLifetime
{
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();
    private const decimal Amount = 80m;

    private static readonly TransferSagaOptions ShortTimeouts = new()
    {
        FraudCheckTimeout = TimeSpan.FromSeconds(1),
        LedgerTimeout = TimeSpan.FromSeconds(1),
    };

    private readonly ServiceProvider _provider = SagaTestHost.Build(ShortTimeouts, inactivityTimeout: TimeSpan.FromSeconds(5));
    private readonly Guid _transferId = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ITestHarness Harness => _provider.GetRequiredService<ITestHarness>();

    private ISagaStateMachineTestHarness<TransferStateMachine, TransferState> Saga =>
        Harness.GetSagaStateMachineHarness<TransferStateMachine, TransferState>();

    private TransferStateMachine Machine => Saga.StateMachine;

    public async ValueTask InitializeAsync() => await Harness.Start();

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task FraudCheckWithoutResponse_ShouldFailWithoutAskingTheLedger()
    {
        await StartTransferAsync();

        Assert.True(await Harness.Published.Any<TransferFailed>(
            m => m.Context.Message.Reason == TransferStateMachine.FraudCheckTimeoutReason, Ct));
        Assert.False(await Harness.Published.Any<DebitAccount>(Ct));
        Assert.Null(await Saga.NotExists(_transferId));
    }

    [Fact]
    public async Task DebitWithoutResponse_ShouldAskTheLedgerToCancel_AndFailWhenCancelled()
    {
        await StartTransferAsync();
        await PublishAndWaitForStateAsync(new TransferFraudCheckApproved(_transferId), Machine.AwaitingDebit);

        await WaitForStateAsync(Machine.AwaitingCancellation);
        Assert.True(await Harness.Published.Any<CancelTransferPosting>(m => m.Context.Message.TransferId == _transferId, Ct));

        await Harness.Bus.Publish(new TransferPostingCancelled(_transferId, Refunded: false), Ct);

        Assert.True(await Harness.Published.Any<TransferFailed>(
            m => m.Context.Message.Reason == TransferStateMachine.LedgerTimeoutReason, Ct));
    }

    [Fact]
    public async Task CreditWithoutResponse_ShouldNotRefundByItself_AndFailAsRefundedWhenTheLedgerRefunds()
    {
        await StartTransferAsync();
        await PublishAndWaitForStateAsync(new TransferFraudCheckApproved(_transferId), Machine.AwaitingDebit);
        await PublishAndWaitForStateAsync(Debited(Source), Machine.AwaitingCredit);

        await WaitForStateAsync(Machine.AwaitingCancellation);

        // Resposta tardia que chega antes do desfecho do cancelamento é ignorada
        await Harness.Bus.Publish(new AccountCreditRejected(_transferId, Destination, "Conta de destino bloqueada"), Ct);
        await Harness.Bus.Publish(new TransferPostingCancelled(_transferId, Refunded: true), Ct);

        Assert.True(await Harness.Published.Any<TransferFailed>(
            m => m.Context.Message.Reason == TransferStateMachine.RefundedReasonPrefix + TransferStateMachine.LedgerTimeoutReason,
            Ct));
        Assert.DoesNotContain(Harness.Consumed.Select<AccountCreditRejected>(Ct), m => m.Exception is not null);

        // A saga não estorna por conta própria (a recusa tardia não virou compensação): quem decide é o Ledger
        Assert.False(await Harness.Published.Any<CreditAccount>(m => m.Context.Message.AccountId == Source, Ct));
    }

    [Fact]
    public async Task CreditWithoutResponse_WhenTheLedgerAlreadyCredited_ShouldComplete()
    {
        await StartTransferAsync();
        await PublishAndWaitForStateAsync(new TransferFraudCheckApproved(_transferId), Machine.AwaitingDebit);
        await PublishAndWaitForStateAsync(Debited(Source), Machine.AwaitingCredit);
        await WaitForStateAsync(Machine.AwaitingCancellation);

        await Harness.Bus.Publish(new TransferPostingAlreadyCompleted(_transferId), Ct);

        Assert.True(await Harness.Published.Any<TransferCompleted>(m => m.Context.Message.TransferId == _transferId, Ct));
        Assert.False(await Harness.Published.Any<TransferFailed>(Ct));
    }

    [Fact]
    public async Task ResponsesInTime_ShouldCancelTheScheduledTimeouts()
    {
        await StartTransferAsync();
        await PublishAndWaitForStateAsync(new TransferFraudCheckApproved(_transferId), Machine.AwaitingDebit);
        await PublishAndWaitForStateAsync(Debited(Source), Machine.AwaitingCredit);
        await Harness.Bus.Publish(new AccountCredited(Guid.NewGuid(), Destination, Amount, _transferId, DateTimeOffset.UtcNow), Ct);
        Assert.True(await Harness.Published.Any<TransferCompleted>(Ct));

        // Depois dos prazos, nenhum timeout dispara
        await Task.Delay(ShortTimeouts.LedgerTimeout * 2, Ct);

        Assert.False(await Harness.Published.Any<CancelTransferPosting>(Ct));
        Assert.False(await Harness.Published.Any<TransferFailed>(Ct));
    }

    private AccountDebited Debited(Guid accountId) =>
        new(Guid.NewGuid(), accountId, Amount, _transferId, DateTimeOffset.UtcNow);

    private Task StartTransferAsync() =>
        PublishAndWaitForStateAsync(
            new TransferRequested(_transferId, Source, Destination, Amount, DateTimeOffset.UtcNow),
            Machine.AwaitingFraudCheck);

    private async Task PublishAndWaitForStateAsync<TMessage>(TMessage message, State expectedState)
        where TMessage : class
    {
        await Harness.Bus.Publish(message, Ct);
        await WaitForStateAsync(expectedState);
    }

    private async Task WaitForStateAsync(State expectedState) =>
        Assert.NotNull(await Saga.Exists(_transferId, _ => expectedState, TimeSpan.FromSeconds(10)));
}
