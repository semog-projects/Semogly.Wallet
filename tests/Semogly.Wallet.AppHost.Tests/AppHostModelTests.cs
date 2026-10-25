using Semogly.Wallet.AppHost;

namespace Semogly.Wallet.AppHost.Tests;

/// <summary>
/// Valida o modelo de recursos do AppHost sem subir containers (não depende de Docker).
/// </summary>
public class AppHostModelTests
{
    [Theory]
    [InlineData(ResourceNames.Postgres)]
    [InlineData(ResourceNames.AccountsDb)]
    [InlineData(ResourceNames.TransfersDb)]
    [InlineData(ResourceNames.LedgerDb)]
    [InlineData(ResourceNames.RabbitMq)]
    [InlineData(ResourceNames.Redis)]
    public async Task AppHost_ShouldDeclareInfrastructureResource(string resourceName)
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Semogly_Wallet_AppHost>(TestContext.Current.CancellationToken);

        Assert.Contains(builder.Resources, r => r.Name == resourceName);
    }

    [Fact]
    public async Task AppHost_ShouldUseDatabasePerService()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Semogly_Wallet_AppHost>(TestContext.Current.CancellationToken);

        var databases = builder.Resources.OfType<PostgresDatabaseResource>().ToList();

        Assert.Equal(3, databases.Count);
        Assert.All(databases, db => Assert.Equal(ResourceNames.Postgres, db.Parent.Name));
    }
}
