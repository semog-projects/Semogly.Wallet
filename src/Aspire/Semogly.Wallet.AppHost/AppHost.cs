using Semogly.Wallet.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Infraestrutura compartilhada. Containers persistentes evitam recriação a cada execução local.
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

// Database-per-service: cada microsserviço é dono exclusivo do seu banco
var accountsDb = postgres.AddDatabase(ResourceNames.AccountsDb);
postgres.AddDatabase(ResourceNames.TransfersDb);
postgres.AddDatabase(ResourceNames.LedgerDb);

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

builder.Build().Run();
