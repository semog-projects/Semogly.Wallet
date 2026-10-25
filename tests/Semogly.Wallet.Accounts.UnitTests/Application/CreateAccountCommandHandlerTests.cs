using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Application.Accounts.CreateAccount;
using Semogly.Wallet.Accounts.Domain.Accounts;
using Semogly.Wallet.Accounts.UnitTests.Fakes;
using Semogly.Wallet.Contracts.Accounts;

namespace Semogly.Wallet.Accounts.UnitTests.Application;

public class CreateAccountCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAccountRepository _repository = new();
    private readonly FakeEventPublisher _publisher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateAccountCommandHandler CreateHandler() =>
        new(_repository, _publisher, _unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task HandleAsync_WithNewDocument_ShouldCreateAccountAndPublishEvent()
    {
        var command = new CreateAccountCommand("Maria Silva", "123.456.789-09", "maria@email.com");

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var account = Assert.Single(_repository.Accounts);
        Assert.Equal(account.Id, result.Value.Id);
        var published = Assert.IsType<AccountCreated>(Assert.Single(_publisher.Published));
        Assert.Equal(account.Id, published.AccountId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task HandleAsync_WithExistingDocument_ShouldReturnConflictWithoutSaving()
    {
        _repository.Add(Account.Open("Maria Silva", "12345678909", "maria@email.com", Now));
        var command = new CreateAccountCommand("Outra Pessoa", "123.456.789-09", "outra@email.com");

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(AccountErrors.DocumentAlreadyRegistered, result.Error);
        Assert.Empty(_publisher.Published);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
