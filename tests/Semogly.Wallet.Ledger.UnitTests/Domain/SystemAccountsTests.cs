using Semogly.Wallet.Ledger.Domain.Accounts;

namespace Semogly.Wallet.Ledger.UnitTests.Domain;

public class SystemAccountsTests
{
    [Fact]
    public void ClearingFor_ShouldAlwaysPickTheSamePartitionForATransfer()
    {
        var transferId = Guid.CreateVersion7();

        Assert.Equal(SystemAccounts.ClearingFor(transferId), SystemAccounts.ClearingFor(transferId));
        Assert.True(SystemAccounts.IsClearing(SystemAccounts.ClearingFor(transferId)));
    }

    [Fact]
    public void ClearingFor_ShouldSpreadTransfersAcrossAllPartitions()
    {
        var partitions = Enumerable.Range(0, 2_000)
            .Select(_ => SystemAccounts.ClearingFor(Guid.CreateVersion7()))
            .GroupBy(id => id)
            .ToList();

        Assert.Equal(SystemAccounts.ClearingPartitions, partitions.Count);

        // Distribuição aproximadamente uniforme: nenhuma partição concentra o tráfego
        Assert.All(partitions, partition => Assert.InRange(partition.Count(), 150, 350));
    }

    [Fact]
    public void IsClearing_ShouldRecognizeTheLegacyAccountButNotOtherSystemAccounts()
    {
        Assert.True(SystemAccounts.IsClearing(SystemAccounts.LegacyClearing));
        Assert.False(SystemAccounts.IsClearing(SystemAccounts.CashIn));
        Assert.False(SystemAccounts.IsClearing(Guid.NewGuid()));
    }
}
