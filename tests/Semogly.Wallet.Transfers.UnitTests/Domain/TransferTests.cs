using Semogly.Wallet.SharedKernel.Domain;
using Semogly.Wallet.Transfers.Domain.Transfers;

namespace Semogly.Wallet.Transfers.UnitTests.Domain;

public class TransferTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();

    [Fact]
    public void Request_WithValidData_ShouldCreatePendingTransfer()
    {
        var transfer = Transfer.Request(Source, Destination, 150.75m, "key-1", Now);

        Assert.Equal(TransferStatus.Pending, transfer.Status);
        Assert.Equal(150.75m, transfer.Amount);
        Assert.Equal(Now, transfer.CreatedAt);
        Assert.Null(transfer.FailureReason);
    }

    [Fact]
    public void Request_ToSameAccount_ShouldThrow() =>
        Assert.Throws<DomainException>(() => Transfer.Request(Source, Source, 10m, "key-1", Now));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.001)]
    public void Request_WithInvalidAmount_ShouldThrow(decimal amount) =>
        Assert.Throws<DomainException>(() => Transfer.Request(Source, Destination, amount, "key-1", Now));

    [Fact]
    public void Complete_WhenPending_ShouldMarkAsCompleted()
    {
        var transfer = Transfer.Request(Source, Destination, 10m, "key-1", Now);

        transfer.Complete(Now.AddSeconds(1));

        Assert.Equal(TransferStatus.Completed, transfer.Status);
        Assert.Equal(Now.AddSeconds(1), transfer.UpdatedAt);
    }

    [Fact]
    public void Fail_WhenPending_ShouldStoreReason()
    {
        var transfer = Transfer.Request(Source, Destination, 10m, "key-1", Now);

        transfer.Fail("Saldo insuficiente", Now);

        Assert.Equal(TransferStatus.Failed, transfer.Status);
        Assert.Equal("Saldo insuficiente", transfer.FailureReason);
    }

    [Fact]
    public void Complete_WhenAlreadyFailed_ShouldThrow()
    {
        var transfer = Transfer.Request(Source, Destination, 10m, "key-1", Now);
        transfer.Fail("Fraude", Now);

        Assert.Throws<DomainException>(() => transfer.Complete(Now));
    }
}
