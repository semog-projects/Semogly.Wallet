using Semogly.Wallet.Accounts.Application.Accounts.LookupAccount;

namespace Semogly.Wallet.Accounts.UnitTests.Application;

public class LookupAccountQueryHandlerTests
{
    [Theory]
    [InlineData("Maria Souza Lima", "Maria L.")]
    [InlineData("joão destino", "joão D.")]
    [InlineData("Cher", "Cher")]
    [InlineData("  Ana   Paula  ", "Ana P.")]
    public void MaskName_ShouldShowFirstNameAndLastInitial(string fullName, string expected) =>
        Assert.Equal(expected, LookupAccountQueryHandler.MaskName(fullName));
}
