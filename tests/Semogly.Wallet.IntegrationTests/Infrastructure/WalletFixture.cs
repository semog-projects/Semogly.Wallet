using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Semogly.Wallet.Accounts.Api;
using Semogly.Wallet.Fraud.Api;
using Semogly.Wallet.Gateway;
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

    // Mesmo realm versionado que o AppHost importa
    private readonly KeycloakContainer _keycloak = new KeycloakBuilder("quay.io/keycloak/keycloak:26.6")
        .WithUsername(KeycloakAdmin)
        .WithPassword(KeycloakAdminPassword)
        .WithRealm(Path.Combine(AppContext.BaseDirectory, "Realms", "semogly-wallet-realm.json"))
        .Build();

    private const string KeycloakAdmin = "admin";
    private const string KeycloakAdminPassword = "admin";

    /// <summary>Operador dos testes: acesso amplo, usado para preparar contas, depósitos e transferências.</summary>
    public const string OperatorUsername = "operador.testes@semogly.dev";

    private (string Token, DateTimeOffset IssuedAt)? _operatorToken;

    private readonly List<IAsyncDisposable> _factories = [];

    public WebApplicationFactory<AccountsApiMarker> Accounts { get; private set; } = null!;
    public WebApplicationFactory<TransfersApiMarker> Transfers { get; private set; } = null!;
    public WebApplicationFactory<LedgerApiMarker> Ledger { get; private set; } = null!;
    public WebApplicationFactory<FraudApiMarker> Fraud { get; private set; } = null!;
    public WebApplicationFactory<NotificationsApiMarker> Notifications { get; private set; } = null!;
    public WebApplicationFactory<GatewayMarker> Gateway { get; private set; } = null!;

    /// <summary>Usuários e tokens reais do Keycloak.</summary>
    public KeycloakTestUsers Keycloak { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync(), _redis.StartAsync(), _keycloak.StartAsync());
        Keycloak = new KeycloakTestUsers(new Uri(_keycloak.GetBaseAddress()), KeycloakAdmin, KeycloakAdminPassword);

        // Retorno do login OIDC no Gateway em memória (BffTests usa https://localhost)
        await Keycloak.AllowGatewayRedirectUriAsync("https://localhost/*");
        await Keycloak.CreateAsync(OperatorUsername, OperatorUsername, "operator");
        await CreateDatabasesAsync();

        // Mesmos nomes de connection string que o AppHost injeta
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:rabbitmq"] = _rabbitMq.GetConnectionString(),
            ["ConnectionStrings:redis"] = _redis.GetConnectionString(),

            // Endereço do Keycloak para o service discovery (validação de tokens e password grant do Gateway)
            ["services:keycloak:http:0"] = _keycloak.GetBaseAddress().TrimEnd('/'),

            // Consulta de posse das contas: o endereço é resolvido, mas a chamada vai ao Accounts em memória
            [$"services:{AccountAccessExtensions.AccountsResource}:http:0"] = "http://accounts-api",
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

        // Gateway: aqui exercitado pelo hub SignalR (o proxy depende do service discovery do Aspire)
        Gateway = Host<GatewayMarker>(settings);

        // Força a inicialização dos hosts (migrations, schema do Marten e conexão com o broker)
        _ = Accounts.Server;
        _ = Transfers.Server;
        _ = Ledger.Server;
        _ = Fraud.Server;
        _ = Notifications.Server;
        _ = Gateway.Server;
    }

    /// <summary>Cliente com o token do operador dos testes.</summary>
    public HttpClient AsOperator<TEntry>(WebApplicationFactory<TEntry> factory)
        where TEntry : class =>
        factory.CreateDefaultClient(new BearerTokenHandler(OperatorTokenAsync));

    /// <summary>Cliente com o token do usuário (precisa existir no Keycloak).</summary>
    public HttpClient As<TEntry>(WebApplicationFactory<TEntry> factory, string username)
        where TEntry : class =>
        factory.CreateDefaultClient(new BearerTokenHandler(() => Keycloak.TokenForAsync(username)));

    private async Task<string> OperatorTokenAsync()
    {
        // Tokens valem 30 minutos no realm; renovar a cada minuto evita um password grant por requisição
        if (_operatorToken is not { } cached || DateTimeOffset.UtcNow - cached.IssuedAt > TimeSpan.FromMinutes(1))
        {
            cached = (await Keycloak.TokenForAsync(OperatorUsername), DateTimeOffset.UtcNow);
            _operatorToken = cached;
        }

        return cached.Token;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }

        Keycloak?.Dispose();

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _keycloak.DisposeAsync().AsTask());
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

            // Sem rede entre os hosts em memória: a consulta de posse vai direto ao TestServer do Accounts
            builder.ConfigureTestServices(services => services
                .AddHttpClient(AccountAccessExtensions.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Accounts.Server.CreateHandler()));
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
