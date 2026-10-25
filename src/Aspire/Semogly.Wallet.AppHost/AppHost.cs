using Semogly.Wallet.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Infraestrutura compartilhada. Containers persistentes evitam recriação a cada execução local.
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

// Database-per-service: cada microsserviço é dono exclusivo do seu banco
postgres.AddDatabase(ResourceNames.AccountsDb);
postgres.AddDatabase(ResourceNames.TransfersDb);
postgres.AddDatabase(ResourceNames.LedgerDb);

builder.AddRabbitMQ(ResourceNames.RabbitMq)
    .WithDataVolume()
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddRedis(ResourceNames.Redis)
    .WithLifetime(ContainerLifetime.Persistent);

builder.Build().Run();
