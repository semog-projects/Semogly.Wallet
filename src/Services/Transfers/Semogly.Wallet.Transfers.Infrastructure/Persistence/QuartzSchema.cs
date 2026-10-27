namespace Semogly.Wallet.Transfers.Infrastructure.Persistence;

/// <summary>Tabelas do Quartz (agendador dos timeouts da saga), versionadas junto com as migrations do serviço.</summary>
internal static class QuartzSchema
{
    private const string CreateTablesResource = "Semogly.Wallet.Transfers.Infrastructure.Persistence.Scripts.quartz_tables_postgres.sql";

    private static readonly string[] Tables =
    [
        "qrtz_fired_triggers", "qrtz_paused_trigger_grps", "qrtz_scheduler_state", "qrtz_locks",
        "qrtz_simprop_triggers", "qrtz_simple_triggers", "qrtz_cron_triggers", "qrtz_blob_triggers",
        "qrtz_triggers", "qrtz_job_details", "qrtz_calendars",
    ];

    public static string CreateTablesSql()
    {
        using var stream = typeof(QuartzSchema).Assembly.GetManifestResourceStream(CreateTablesResource)
            ?? throw new InvalidOperationException($"Recurso '{CreateTablesResource}' não encontrado.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public static string DropTablesSql() => string.Join('\n', Tables.Select(table => $"DROP TABLE IF EXISTS {table};"));
}
