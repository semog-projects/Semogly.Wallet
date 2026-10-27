namespace Semogly.Wallet.Ledger.Domain.Accounts;

public enum LedgerAccountType
{
    /// <summary>Conta de cliente: não pode ficar com saldo negativo.</summary>
    Customer,

    /// <summary>Conta interna de contrapartida (ver <see cref="SystemAccounts"/>).</summary>
    System,
}

/// <summary>Contas internas usadas como contrapartida nas partidas dobradas.</summary>
public static class SystemAccounts
{
    /// <summary>
    /// Valores em trânsito entre o débito e o crédito de uma transferência, particionados: toda transferência
    /// lança na conta de trânsito, e uma conta única viraria um stream disputado por todos os lançamentos
    /// simultâneos (conflitos de concorrência em cadeia sob carga). Cada transferência usa sempre a mesma
    /// partição (débito, crédito e estorno), que volta a zero quando ela termina.
    /// </summary>
    public const int ClearingPartitions = 8;

    /// <summary>Conta de trânsito única, anterior às partições: mantida no livro (saldo zero).</summary>
    public static readonly Guid LegacyClearing = new("00000000-0000-0000-0000-000000000001");

    private static readonly Guid[] ClearingPartitionIds = Enumerable.Range(0, ClearingPartitions)
        .Select(index => new Guid($"00000000-0000-0000-0001-{index:x12}"))
        .ToArray();

    /// <summary>Partição da conta de trânsito da transferência (pelo último byte do Id, aleatório no UUID v7).</summary>
    public static Guid ClearingFor(Guid transferId) => ClearingPartitionIds[transferId.ToByteArray()[^1] % ClearingPartitions];

    public static bool IsClearing(Guid accountId) => accountId == LegacyClearing || ClearingPartitionIds.Contains(accountId);

    /// <summary>Entrada de dinheiro externo (depósitos). Seu saldo negativo é o total depositado na carteira.</summary>
    public static readonly Guid CashIn = new("00000000-0000-0000-0000-000000000002");
}
