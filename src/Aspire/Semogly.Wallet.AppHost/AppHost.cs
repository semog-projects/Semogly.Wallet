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

// Identity Provider: realm importado do arquivo a cada execução (sem volume, sempre igual ao versionado).
// Porta fixa: o emissor dos tokens (iss) é o endereço usado pelos clientes e precisa ser estável entre
// execuções. HTTP em desenvolvimento, como o Gateway para o app: o emulador Android não confia no
// certificado de desenvolvimento do .NET (o app alcança a porta com "adb reverse tcp:8080 tcp:8080").
#pragma warning disable ASPIRECERTIFICATES001 // WithoutHttpsCertificate é experimental no Aspire 13
var keycloak = builder.AddKeycloak(ResourceNames.Keycloak)
    // Sem o certificado de desenvolvimento, o endpoint "http" continua HTTP (com ele, vira HTTPS na 8443)
    .WithoutHttpsCertificate()
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 8080;
        endpoint.TargetPort = 8080;

        // Publicado direto pelo Docker: atrás do proxy do Aspire, a porta fixa não chegava ao container
        endpoint.IsProxied = false;
    })
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    // Emissor fixo independentemente do endereço usado na chamada (navegador, app, serviços via service
    // discovery); chamadas de servidor continuam podendo usar o endereço interno
    .WithEnvironment("KC_HOSTNAME", "http://localhost:8080")
    .WithEnvironment("KC_HOSTNAME_BACKCHANNEL_DYNAMIC", "true")
    .WithRealmImport("./Realms");
#pragma warning restore ASPIRECERTIFICATES001

// Microsserviços
var accountsApi = builder.AddProject<Projects.Semogly_Wallet_Accounts_Api>(ResourceNames.AccountsApi)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(accountsDb).WaitFor(accountsDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health");

// Os demais serviços validam o JWT (Keycloak) e consultam a posse das contas no Accounts
var transfersApi = builder.AddProject<Projects.Semogly_Wallet_Transfers_Api>(ResourceNames.TransfersApi)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(accountsApi).WaitFor(accountsApi)
    .WithReference(transfersDb).WaitFor(transfersDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

var ledgerApi = builder.AddProject<Projects.Semogly_Wallet_Ledger_Api>(ResourceNames.LedgerApi)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(accountsApi).WaitFor(accountsApi)
    .WithReference(ledgerDb).WaitFor(ledgerDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

var fraudApi = builder.AddProject<Projects.Semogly_Wallet_Fraud_Api>(ResourceNames.FraudApi)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(accountsApi).WaitFor(accountsApi)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

var notificationsApi = builder.AddProject<Projects.Semogly_Wallet_Notifications_Api>(ResourceNames.NotificationsApi)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(accountsApi).WaitFor(accountsApi)
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
    .WithReference(keycloak).WaitFor(keycloak)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// Backoffice web (React + Vite): o dev server faz proxy de /api, /bff e /hubs para o Gateway,
// então navegador e Gateway ficam na mesma origem e o cookie do BFF funciona sem CORS
builder.AddViteApp(ResourceNames.Backoffice, "../../../apps/web")
    .WithNpm(install: true)
    // Porta fixa: o retorno do login OIDC (redirect URI) precisa estar cadastrado no Keycloak
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithReference(gateway).WaitFor(gateway)
    .WithExternalHttpEndpoints();

builder.Build().Run();
