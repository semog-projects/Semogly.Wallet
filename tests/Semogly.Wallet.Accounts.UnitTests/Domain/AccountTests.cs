using Semogly.Wallet.Accounts.Domain.Accounts;
using Semogly.Wallet.SharedKernel.Domain;

namespace Semogly.Wallet.Accounts.UnitTests.Domain;

public class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Open_ShouldCreateActiveAccountWithZeroBalance()
    {
        var account = Account.Open("  Maria Silva ", "123.456.789-09", " Maria@Email.com ", Now);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Maria Silva", account.OwnerName);
        Assert.Equal("12345678909", account.Document);
        Assert.Equal("maria@email.com", account.Email);
        Assert.Equal(Account.DefaultCurrency, account.Currency);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(Now, account.CreatedAt);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123.456.789-0")]
    [InlineData("1234567890123")]
    public void Open_WithInvalidDocument_ShouldThrowDomainException(string document)
    {
        Assert.Throws<DomainException>(() => Account.Open("Maria Silva", document, "maria@email.com", Now));
    }

    [Fact]
    public void ApplyCreditAndDebit_ShouldUpdateBalance()
    {
        var account = Account.Open("Maria Silva", "12345678909", "maria@email.com", Now);
        var later = Now.AddMinutes(5);

        account.ApplyCredit(150.75m, Now);
        account.ApplyDebit(50.25m, later);

        Assert.Equal(100.50m, account.Balance);
        Assert.Equal(later, account.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ApplyDebit_WithNonPositiveAmount_ShouldThrowDomainException(decimal amount)
    {
        var account = Account.Open("Maria Silva", "12345678909", "maria@email.com", Now);

        Assert.Throws<DomainException>(() => account.ApplyDebit(amount, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ApplyCredit_WithNonPositiveAmount_ShouldThrowDomainException(decimal amount)
    {
        var account = Account.Open("Maria Silva", "12345678909", "maria@email.com", Now);

        Assert.Throws<DomainException>(() => account.ApplyCredit(amount, Now));
    }
}
