using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Semogly.Wallet.Accounts.Api;
using Semogly.Wallet.Fraud.Api;
using Semogly.Wallet.Ledger.Api;
using Semogly.Wallet.Notifications.Api;
using Semogly.Wallet.Transfers.Api;

namespace Semogly.Wallet.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe Postgres, RabbitMQ e Redis em containers e hospeda os cinco serviços em memória,
/// conectados a eles como no AppHost. Compartilhado por todos os testes (inicialização cara).
/// </summary>
public sealed class WalletFixture : IAsyncLifetime
{
    private static readonly string[] Databases = ["accounts-db", "transfers-db", "ledger-db", "notifications-db"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.3-management").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:8.6").Build();

    private readonly List<IAsyncDisposable> _factories = [];

    public WebApplicationFactory<AccountsApiMarker> Accounts { get; private set; } = null!;
    public WebApplicationFactory<TransfersApiMarker> Transfers { get; private set; } = null!;
    public WebApplicationFactory<LedgerApiMarker> Ledger { get; private set; } = null!;
    public WebApplicationFactory<FraudApiMarker> Fraud { get; private set; } = null!;
    public WebApplicationFactory<NotificationsApiMarker> Notifications { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync(), _redis.StartAsync());
        await CreateDatabasesAsync();

        // Mesmos nomes de connection string que o AppHost injeta
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:rabbitmq"] = _rabbitMq.GetConnectionString(),
            ["ConnectionStrings:redis"] = _redis.GetConnectionString(),
        };

        foreach (var database in Databases)
        {
            settings[$"ConnectionStrings:{database}"] =
                new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;
        }

        Accounts = Host<AccountsApiMarker>(settings);
        Transfers = Host<TransfersApiMarker>(settings);
        Ledger = Host<LedgerApiMarker>(settings);
        Fraud = Host<FraudApiMarker>(settings);
        Notifications = Host<NotificationsApiMarker>(settings);

        // Força a inicialização dos hosts (migrations, schema do Marten e conexão com o broker)
        _ = Accounts.Server;
        _ = Transfers.Server;
        _ = Ledger.Server;
        _ = Fraud.Server;
        _ = Notifications.Server;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }

    private WebApplicationFactory<TEntry> Host<TEntry>(Dictionary<string, string?> settings)
        where TEntry : class
    {
        var factory = new WebApplicationFactory<TEntry>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

        _factories.Add(factory);
        return factory;
    }

    private async Task CreateDatabasesAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        foreach (var database in Databases)
        {
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class WalletTestGroup : ICollectionFixture<WalletFixture>
{
    public const string Name = "Wallet";
}
