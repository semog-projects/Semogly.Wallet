using System.Net;
using Semogly.Wallet.IntegrationTests.Infrastructure;
using Semogly.Wallet.Notifications.Domain.Notifications;
using Semogly.Wallet.Transfers.Domain.Transfers;
using Semogly.Wallet.Transfers.Infrastructure.Sagas;

namespace Semogly.Wallet.IntegrationTests;

/// <summary>
/// Fluxo completo entre os serviços reais: Transfers (saga) → Fraud → Ledger → Accounts/Notifications.
/// </summary>
[Collection(WalletTestGroup.Name)]
public sealed class TransferFlowTests(WalletFixture fixture)
{
    private readonly WalletApi _api = new(fixture);

    [Fact]
    public async Task Transfer_WithFunds_ShouldCompleteAndUpdateEveryService()
    {
        var ana = await _api.OpenAccountAsync("Ana Souza", initialDeposit: 500m);
        var bruno = await _api.OpenAccountAsync("Bruno Lima");

        var (status, transfer) = await _api.RequestTransferAsync(ana, bruno, 200m);
        Assert.Equal(HttpStatusCode.Accepted, status);

        var outcome = await _api.WaitForOutcomeAsync(transfer!.Id);
        Assert.Equal(TransferStatus.Completed, outcome.Status);

        // Ledger é a fonte da verdade; Accounts e Notifications convergem pelos eventos
        Assert.Equal(300m, await _api.GetLedgerBalanceAsync(ana));
        Assert.Equal(200m, await _api.GetLedgerBalanceAsync(bruno));
        await Eventually.GetAsync(() => _api.GetBalanceAsync(ana), balance => balance == 300m, "saldo de Ana no Accounts");
        await Eventually.GetAsync(() => _api.GetBalanceAsync(bruno), balance => balance == 200m, "saldo de Bruno no Accounts");
        await Eventually.GetAsync(
            () => _api.GetNotificationsAsync(bruno),
            notifications => notifications.Any(n => n.Type == NotificationType.TransferReceived),
            "notificação de recebimento para Bruno");
    }

    [Fact]
    public async Task Transfer_WhenCreditIsRejected_ShouldRefundSource()
    {
        var ana = await _api.OpenAccountAsync("Ana Estorno", initialDeposit: 100m);
        var unknownAccount = Guid.NewGuid();

        var (_, transfer) = await _api.RequestTransferAsync(ana, unknownAccount, 40m);
        var outcome = await _api.WaitForOutcomeAsync(transfer!.Id);

        // Compensação da saga: o débito já efetivado é estornado para a origem
        Assert.Equal(TransferStatus.Failed, outcome.Status);
        Assert.StartsWith(TransferStateMachine.RefundedReasonPrefix, outcome.FailureReason, StringComparison.Ordinal);
        Assert.Equal(100m, await _api.GetLedgerBalanceAsync(ana));
        await Eventually.GetAsync(() => _api.GetBalanceAsync(ana), balance => balance == 100m, "saldo estornado no Accounts");
    }

    [Fact]
    public async Task Transfer_WithInsufficientFunds_ShouldFailWithoutMovingMoney()
    {
        var ana = await _api.OpenAccountAsync("Ana Sem Saldo", initialDeposit: 50m);
        var bruno = await _api.OpenAccountAsync("Bruno Sem Saldo");

        var (_, transfer) = await _api.RequestTransferAsync(ana, bruno, 80m);
        var outcome = await _api.WaitForOutcomeAsync(transfer!.Id);

        Assert.Equal(TransferStatus.Failed, outcome.Status);
        Assert.Equal("Saldo insuficiente.", outcome.FailureReason);
        Assert.Equal(50m, await _api.GetLedgerBalanceAsync(ana));
        Assert.Equal(0m, await _api.GetLedgerBalanceAsync(bruno));
    }

    [Fact]
    public async Task Transfer_AboveTransactionLimit_ShouldBeRejectedByFraud()
    {
        var ana = await _api.OpenAccountAsync("Ana Limite", initialDeposit: 9_000m);
        var bruno = await _api.OpenAccountAsync("Bruno Limite");

        var (_, transfer) = await _api.RequestTransferAsync(ana, bruno, 6_000m);
        var outcome = await _api.WaitForOutcomeAsync(transfer!.Id);

        Assert.Equal(TransferStatus.Failed, outcome.Status);
        Assert.Equal("Valor acima do limite por transação.", outcome.FailureReason);
        Assert.Equal(9_000m, await _api.GetLedgerBalanceAsync(ana));
    }

    [Fact]
    public async Task ConcurrentRequests_WithSameIdempotencyKey_ShouldMoveMoneyOnce()
    {
        var ana = await _api.OpenAccountAsync("Ana Idempotente", initialDeposit: 100m);
        var bruno = await _api.OpenAccountAsync("Bruno Idempotente");
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => _api.RequestTransferAsync(ana, bruno, 30m, key)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.Status));
        var transferId = Assert.Single(responses.Select(r => r.Transfer!.Id).Distinct());

        Assert.Equal(TransferStatus.Completed, (await _api.WaitForOutcomeAsync(transferId)).Status);
        Assert.Equal(70m, await _api.GetLedgerBalanceAsync(ana));
        Assert.Equal(30m, await _api.GetLedgerBalanceAsync(bruno));
    }
}
