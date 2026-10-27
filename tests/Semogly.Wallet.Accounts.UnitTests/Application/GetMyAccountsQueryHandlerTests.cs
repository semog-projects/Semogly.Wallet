using Semogly.Wallet.Accounts.Application.Accounts.GetMyAccounts;

namespace Semogly.Wallet.Accounts.UnitTests.Application;

public class GetMyAccountsQueryHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithoutEmailClaim_ShouldExplainTheProblem(string? email)
    {
        // Sem e-mail no token não há como identificar o titular; o read service nem é consultado
        var handler = new GetMyAccountsQueryHandler(readService: null!);

        var result = await handler.HandleAsync(new GetMyAccountsQuery(email), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(GetMyAccountsQueryHandler.MissingEmail, result.Error);
    }
}
