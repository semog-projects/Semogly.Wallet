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
    [InlineData(ResourceNames.NotificationsDb)]
    [InlineData(ResourceNames.RabbitMq)]
    [InlineData(ResourceNames.Redis)]
    public async Task AppHost_ShouldDeclareInfrastructureResource(string resourceName)
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Semogly_Wallet_AppHost>(TestContext.Current.CancellationToken);

        Assert.Contains(builder.Resources, r => r.Name == resourceName);
    }

    [Theory]
    [InlineData(ResourceNames.AccountsApi, new[] { ResourceNames.AccountsDb, ResourceNames.RabbitMq, ResourceNames.Redis })]
    [InlineData(ResourceNames.TransfersApi, new[] { ResourceNames.TransfersDb, ResourceNames.RabbitMq })]
    [InlineData(ResourceNames.LedgerApi, new[] { ResourceNames.LedgerDb, ResourceNames.RabbitMq })]
    [InlineData(ResourceNames.FraudApi, new[] { ResourceNames.Redis, ResourceNames.RabbitMq })]
    [InlineData(ResourceNames.NotificationsApi, new[] { ResourceNames.NotificationsDb, ResourceNames.RabbitMq })]
    [InlineData(ResourceNames.Gateway, new[]
    {
        ResourceNames.AccountsApi, ResourceNames.TransfersApi, ResourceNames.LedgerApi,
        ResourceNames.FraudApi, ResourceNames.NotificationsApi, ResourceNames.RabbitMq, ResourceNames.Redis,
    })]
    public async Task Service_ShouldReferenceItsInfrastructure(string serviceName, string[] expectedReferences)
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Semogly_Wallet_AppHost>(TestContext.Current.CancellationToken);

        var service = Assert.Single(builder.Resources.OfType<ProjectResource>(), r => r.Name == serviceName);
        var referencedResources = service.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(a => a.Type == "Reference")
            .Select(a => a.Resource.Name)
            .ToList();

        Assert.All(expectedReferences, expected => Assert.Contains(expected, referencedResources));
    }

    [Fact]
    public async Task Backoffice_ShouldBeAViteAppReferencingTheGateway()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Semogly_Wallet_AppHost>(TestContext.Current.CancellationToken);

        var backoffice = Assert.Single(builder.Resources, r => r.Name == ResourceNames.Backoffice);
        var references = backoffice.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(a => a.Type == "Reference")
            .Select(a => a.Resource.Name);

        Assert.Contains(ResourceNames.Gateway, references);
    }

    [Fact]
    public async Task AppHost_ShouldUseDatabasePerService()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Semogly_Wallet_AppHost>(TestContext.Current.CancellationToken);

        var databases = builder.Resources.OfType<PostgresDatabaseResource>().ToList();

        Assert.Equal(4, databases.Count);
        Assert.All(databases, db => Assert.Equal(ResourceNames.Postgres, db.Parent.Name));
    }
}
