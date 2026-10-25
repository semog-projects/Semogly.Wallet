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

var rabbitMq = builder.AddRabbitMQ(ResourceNames.RabbitMq)
    .WithDataVolume()
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis(ResourceNames.Redis)
    .WithLifetime(ContainerLifetime.Persistent);

// Microsserviços
builder.AddProject<Projects.Semogly_Wallet_Accounts_Api>(ResourceNames.AccountsApi)
    .WithReference(accountsDb).WaitFor(accountsDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Semogly_Wallet_Transfers_Api>(ResourceNames.TransfersApi)
    .WithReference(transfersDb).WaitFor(transfersDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Semogly_Wallet_Ledger_Api>(ResourceNames.LedgerApi)
    .WithReference(ledgerDb).WaitFor(ledgerDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
