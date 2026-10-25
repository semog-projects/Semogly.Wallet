namespace Semogly.Wallet.AppHost;

/// <summary>Nomes dos recursos do AppHost, usados também como nomes das connection strings.</summary>
public static class ResourceNames
{
    public const string Postgres = "postgres";
    public const string AccountsDb = "accounts-db";
    public const string TransfersDb = "transfers-db";
    public const string LedgerDb = "ledger-db";
    public const string RabbitMq = "rabbitmq";
    public const string Redis = "redis";
    public const string AccountsApi = "accounts-api";
}
