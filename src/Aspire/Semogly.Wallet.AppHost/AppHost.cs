using Semogly.Wallet.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Infraestrutura compartilhada. Containers persistentes evitam recriação a cada execução local.
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

// Database-per-service: cada microsserviço é dono exclusivo do seu banco
var accountsDb = postgres.AddDatabase(ResourceNames.AccountsDb);
var transfersDb = postgres.AddDatabase(ResourceNames.TransfersDb);
var ledgerDb = postgres.AddDatabase(ResourceNames.LedgerDb);
var notificationsDb = postgres.AddDatabase(ResourceNames.NotificationsDb);

var rabbitMq = builder.AddRabbitMQ(ResourceNames.RabbitMq)
    .WithDataVolume()
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis(ResourceNames.Redis)
    .WithLifetime(ContainerLifetime.Persistent);

// Chave do JWT emitido pelo Gateway, gerada e persistida pelo Aspire (user secrets).
// Também vai para os serviços que validam o token por conta própria (defesa em profundidade).
var jwtSigningKey = builder.AddParameter(
    ResourceNames.JwtSigningKey,
    new GenerateParameterDefault { MinLength = 64, Special = false },
    secret: true,
    persist: true);

// Microsserviços
var accountsApi = builder.AddProject<Projects.Semogly_Wallet_Accounts_Api>(ResourceNames.AccountsApi)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithReference(accountsDb).WaitFor(accountsDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health");

var transfersApi = builder.AddProject<Projects.Semogly_Wallet_Transfers_Api>(ResourceNames.TransfersApi)
    .WithReference(transfersDb).WaitFor(transfersDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

var ledgerApi = builder.AddProject<Projects.Semogly_Wallet_Ledger_Api>(ResourceNames.LedgerApi)
    .WithReference(ledgerDb).WaitFor(ledgerDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

var fraudApi = builder.AddProject<Projects.Semogly_Wallet_Fraud_Api>(ResourceNames.FraudApi)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

var notificationsApi = builder.AddProject<Projects.Semogly_Wallet_Notifications_Api>(ResourceNames.NotificationsApi)
    .WithReference(notificationsDb).WaitFor(notificationsDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

// Gateway: único ponto de entrada externo
var gateway = builder.AddProject<Projects.Semogly_Wallet_Gateway>(ResourceNames.Gateway)
    .WithReference(accountsApi).WaitFor(accountsApi)
    .WithReference(transfersApi).WaitFor(transfersApi)
    .WithReference(ledgerApi).WaitFor(ledgerApi)
    .WithReference(fraudApi).WaitFor(fraudApi)
    .WithReference(notificationsApi).WaitFor(notificationsApi)
    .WithReference(rabbitMq).WaitFor(rabbitMq) // eventos de transferência para o SignalR
    .WithReference(redis).WaitFor(redis)       // backplane do SignalR
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// Backoffice web (React + Vite): o dev server faz proxy de /api, /bff e /hubs para o Gateway,
// então navegador e Gateway ficam na mesma origem e o cookie do BFF funciona sem CORS
builder.AddViteApp(ResourceNames.Backoffice, "../../../apps/web")
    .WithNpm(install: true)
    .WithReference(gateway).WaitFor(gateway)
    .WithExternalHttpEndpoints();

builder.Build().Run();
