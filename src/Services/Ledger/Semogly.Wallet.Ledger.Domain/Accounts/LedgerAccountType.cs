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
    /// <summary>Valores em trânsito entre o débito e o crédito de uma transferência. Volta a zero quando todas terminam.</summary>
    public static readonly Guid Clearing = new("00000000-0000-0000-0000-000000000001");

    /// <summary>Entrada de dinheiro externo (depósitos). Seu saldo negativo é o total depositado na carteira.</summary>
    public static readonly Guid CashIn = new("00000000-0000-0000-0000-000000000002");
}
