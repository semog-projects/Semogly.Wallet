using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Accounts;
using Semogly.Wallet.Ledger.Application.Accounts.Deposit;
using Semogly.Wallet.Ledger.Application.Accounts.OpenAccount;
using Semogly.Wallet.Ledger.Application.Accounts.Transfers;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.Ledger.UnitTests.Fakes;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.UnitTests.Application;

/// <summary>Fluxo completo do livro-razão: abertura, depósito, débito/crédito da saga e estorno.</summary>
public class TransferPostingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeLedgerAccountRepository _repository = new();
    private readonly FakeTransferPostingRepository _postings = new();
    private readonly FakeEventPublisher _publisher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _clock = new(Now);

    private readonly Guid _source = Guid.NewGuid();
    private readonly Guid _destination = Guid.NewGuid();
    private readonly Guid _transferId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task OpenAsync(Guid accountId) =>
        await new OpenLedgerAccountCommandHandler(_repository, _unitOfWork, _clock)
            .HandleAsync(new OpenLedgerAccountCommand(accountId), Ct);

    private async Task DepositAsync(Guid accountId, decimal amount) =>
        await new DepositCommandHandler(_repository, _publisher, _unitOfWork, _clock)
            .HandleAsync(new DepositCommand(accountId, amount), Ct);

    private Task<Result> DebitAsync(Guid accountId, decimal amount) =>
        new DebitForTransferCommandHandler(_repository, _postings, _publisher, _unitOfWork, _clock)
            .HandleAsync(new DebitForTransferCommand(_transferId, accountId, amount), Ct);

    private Task<Result> CreditAsync(Guid accountId, decimal amount) =>
        new CreditForTransferCommandHandler(_repository, _postings, _publisher, _unitOfWork, _clock)
            .HandleAsync(new CreditForTransferCommand(_transferId, accountId, amount), Ct);

    private Task<Result> CancelAsync() =>
        new CancelTransferPostingCommandHandler(_repository, _postings, _publisher, _unitOfWork, _clock)
            .HandleAsync(new CancelTransferPostingCommand(_transferId), Ct);

    private async Task FundedAccountsAsync(decimal deposit)
    {
        await OpenAsync(_source);
        await OpenAsync(_destination);
        await DepositAsync(_source, deposit);
    }

    [Fact]
    public async Task OpenAccount_WhenAlreadyOpen_ShouldBeIdempotent()
    {
        await OpenAsync(_source);
        await OpenAsync(_source);

        Assert.Single(_repository.Streams[_source]);
    }

    [Fact]
    public async Task Deposit_ShouldCreditAccountAgainstCashIn()
    {
        await OpenAsync(_source);

        await DepositAsync(_source, 500m);

        Assert.Equal(500m, _repository.BalanceOf(_source));
        Assert.Equal(-500m, _repository.BalanceOf(SystemAccounts.CashIn));
        var published = Assert.IsType<AccountCredited>(Assert.Single(_publisher.Published));
        Assert.Null(published.TransferId);
    }

    [Fact]
    public async Task Deposit_ToUnknownAccount_ShouldReturnNotFound()
    {
        var result = await new DepositCommandHandler(_repository, _publisher, _unitOfWork, _clock)
            .HandleAsync(new DepositCommand(_source, 100m), Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(LedgerErrors.AccountNotFound(_source), result.Error);
    }

    [Fact]
    public async Task DebitAndCredit_ShouldMoveFundsThroughClearingAndKeepBookBalanced()
    {
        await OpenAsync(_source);
        await OpenAsync(_destination);
        await DepositAsync(_source, 500m);

        await DebitAsync(_source, 200m);
        Assert.Equal(200m, _repository.BalanceOf(SystemAccounts.Clearing));

        await CreditAsync(_destination, 200m);

        Assert.Equal(300m, _repository.BalanceOf(_source));
        Assert.Equal(200m, _repository.BalanceOf(_destination));
        Assert.Equal(0m, _repository.BalanceOf(SystemAccounts.Clearing));
        Assert.Equal(0m, _repository.TotalBalance());
        Assert.Contains(_publisher.Published, e => e is AccountDebited d && d.TransferId == _transferId);
        Assert.Contains(_publisher.Published, e => e is AccountCredited c && c.TransferId == _transferId);
    }

    [Fact]
    public async Task Debit_WithInsufficientFunds_ShouldPublishRejection()
    {
        await OpenAsync(_source);
        await DepositAsync(_source, 50m);

        var result = await DebitAsync(_source, 200m);

        Assert.True(result.IsSuccess);
        var rejected = Assert.IsType<AccountDebitRejected>(_publisher.Published[^1]);
        Assert.Equal(LedgerErrors.InsufficientFundsReason, rejected.Reason);
        Assert.Equal(50m, _repository.BalanceOf(_source));
    }

    [Fact]
    public async Task Debit_FromSystemAccount_ShouldBeRejectedAsUnknown()
    {
        await DebitAsync(SystemAccounts.CashIn, 10m);

        var rejected = Assert.IsType<AccountDebitRejected>(Assert.Single(_publisher.Published));
        Assert.Equal(LedgerErrors.AccountNotFoundReason, rejected.Reason);
    }

    [Fact]
    public async Task Credit_ToUnknownAccount_ShouldPublishRejectionAndRefundShouldRestoreSource()
    {
        await OpenAsync(_source);
        await DepositAsync(_source, 100m);
        await DebitAsync(_source, 100m);

        await CreditAsync(_destination, 100m);
        Assert.IsType<AccountCreditRejected>(_publisher.Published[^1]);

        // Compensação da saga: crédito de volta na origem
        await CreditAsync(_source, 100m);

        Assert.Equal(100m, _repository.BalanceOf(_source));
        Assert.Equal(0m, _repository.BalanceOf(SystemAccounts.Clearing));
        Assert.Equal(0m, _repository.TotalBalance());
    }

    [Fact]
    public async Task Debit_WhenRedelivered_ShouldPostOnlyOnce()
    {
        await FundedAccountsAsync(100m);

        await DebitAsync(_source, 60m);
        await DebitAsync(_source, 60m);

        Assert.Equal(40m, _repository.BalanceOf(_source));
        Assert.Single(_publisher.Published.OfType<AccountDebited>());
    }

    [Fact]
    public async Task Cancel_BeforeDebit_ShouldReportCancelledAndIgnoreLateDebit()
    {
        await FundedAccountsAsync(100m);

        await CancelAsync();
        await DebitAsync(_source, 60m);

        var cancelled = Assert.Single(_publisher.Published.OfType<TransferPostingCancelled>());
        Assert.False(cancelled.Refunded);
        Assert.Equal(100m, _repository.BalanceOf(_source));
        Assert.Empty(_publisher.Published.OfType<AccountDebited>());
    }

    [Fact]
    public async Task Cancel_AfterDebit_ShouldRefundSourceAndIgnoreLateCredit()
    {
        await FundedAccountsAsync(100m);
        await DebitAsync(_source, 60m);

        await CancelAsync();
        await CreditAsync(_destination, 60m);

        Assert.True(Assert.Single(_publisher.Published.OfType<TransferPostingCancelled>()).Refunded);
        Assert.Equal(100m, _repository.BalanceOf(_source));
        Assert.Equal(0m, _repository.BalanceOf(_destination));
        Assert.Equal(0m, _repository.BalanceOf(SystemAccounts.Clearing));
        Assert.Equal(0m, _repository.TotalBalance());

        // O estorno é um crédito na origem (o Accounts atualiza o saldo por ele)
        var refund = Assert.Single(_publisher.Published.OfType<AccountCredited>(), c => c.TransferId == _transferId);
        Assert.Equal(_source, refund.AccountId);
    }

    [Fact]
    public async Task Cancel_AfterCredit_ShouldReportAlreadyCompletedWithoutRefund()
    {
        await FundedAccountsAsync(100m);
        await DebitAsync(_source, 60m);
        await CreditAsync(_destination, 60m);

        await CancelAsync();

        Assert.Single(_publisher.Published.OfType<TransferPostingAlreadyCompleted>());
        Assert.Empty(_publisher.Published.OfType<TransferPostingCancelled>());
        Assert.Equal(40m, _repository.BalanceOf(_source));
        Assert.Equal(60m, _repository.BalanceOf(_destination));
    }

    [Fact]
    public async Task Cancel_WhenRedelivered_ShouldRefundOnlyOnce()
    {
        await FundedAccountsAsync(100m);
        await DebitAsync(_source, 60m);

        await CancelAsync();
        await CancelAsync();

        Assert.Equal(100m, _repository.BalanceOf(_source));
        Assert.Equal(0m, _repository.TotalBalance());
        Assert.All(_publisher.Published.OfType<TransferPostingCancelled>(), c => Assert.True(c.Refunded));
    }
}
