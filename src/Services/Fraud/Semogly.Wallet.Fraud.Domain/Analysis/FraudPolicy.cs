namespace Semogly.Wallet.Fraud.Domain.Analysis;

/// <summary>Parâmetros das regras antifraude (configuráveis por ambiente).</summary>
public sealed class FraudPolicy
{
    public const string SectionName = "FraudPolicy";

    /// <summary>Valor máximo de uma única transferência.</summary>
    public decimal MaxTransactionAmount { get; set; } = 5_000m;

    /// <summary>Soma máxima transferida por conta no mesmo dia (horário de Brasília).</summary>
    public decimal DailyLimit { get; set; } = 10_000m;

    /// <summary>Quantidade máxima de transferências por conta dentro de <see cref="VelocityWindow"/>.</summary>
    public int MaxTransfersPerWindow { get; set; } = 5;

    public TimeSpan VelocityWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Fuso que define a virada do dia para o limite diário.</summary>
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";

    public DateOnly BusinessDate(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);
}
