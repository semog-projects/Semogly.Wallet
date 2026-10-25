using Semogly.Wallet.Accounts.Application.Accounts.ApplyLedgerEntry;
using Semogly.Wallet.Accounts.Domain.Accounts;
using Semogly.Wallet.Accounts.UnitTests.Fakes;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.UnitTests.Application;

public class ApplyLedgerEntryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAccountRepository _repository = new();
    private readonly FakeAccountCache _cache = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private ApplyLedgerEntryCommandHandler CreateHandler() =>
        new(_repository, _cache, _unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task HandleAsync_WithCreditThenDebit_ShouldUpdateBalanceAndInvalidateCache()
    {
        var account = Account.Open("Maria Silva", "12345678909", "maria@email.com", Now);
        _repository.Add(account);
        var handler = CreateHandler();
        var ct = TestContext.Current.CancellationToken;

        await handler.HandleAsync(new ApplyLedgerEntryCommand(account.Id, 250m, LedgerEntryType.Credit), ct);
        var result = await handler.HandleAsync(new ApplyLedgerEntryCommand(account.Id, 75.5m, LedgerEntryType.Debit), ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(174.5m, account.Balance);
        Assert.Equal(2, _unitOfWork.SaveCount);
        Assert.Equal([account.Id, account.Id], _cache.Invalidated);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownAccount_ShouldReturnNotFound()
    {
        var command = new ApplyLedgerEntryCommand(Guid.NewGuid(), 10m, LedgerEntryType.Credit);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_cache.Invalidated);
    }
}
