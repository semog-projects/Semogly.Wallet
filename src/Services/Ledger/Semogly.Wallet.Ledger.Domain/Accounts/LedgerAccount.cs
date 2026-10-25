using Semogly.Wallet.SharedKernel.Domain;

namespace Semogly.Wallet.Ledger.Domain.Accounts;

/// <summary>
/// Conta do livro-razão, com Event Sourcing: o estado é reconstruído a partir dos eventos
/// do stream, e cada operação gera novos eventos pendentes de gravação.
/// </summary>
public sealed class LedgerAccount
{
    private readonly List<object> _pendingEvents = [];

    private LedgerAccount() { }

    public Guid Id { get; private set; }
    public LedgerAccountType Type { get; private set; }
    public decimal Balance { get; private set; }

    /// <summary>Quantidade de eventos já gravados no stream (usada na concorrência otimista).</summary>
    public long PersistedVersion { get; private set; }

    public IReadOnlyList<object> PendingEvents => _pendingEvents;

    public static LedgerAccount Open(Guid id, LedgerAccountType type, DateTimeOffset now)
    {
        var account = new LedgerAccount();
        account.Raise(new LedgerAccountOpened(id, type, now));
        return account;
    }

    /// <summary>Reconstrói a conta a partir dos eventos gravados.</summary>
    public static LedgerAccount Rehydrate(IEnumerable<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var account = new LedgerAccount();

        foreach (var @event in events)
        {
            account.Apply(@event);
            account.PersistedVersion++;
        }

        return account.PersistedVersion > 0
            ? account
            : throw new ArgumentException("O stream da conta não possui eventos.", nameof(events));
    }

    /// <summary>Contas de sistema podem ficar negativas; contas de cliente, não.</summary>
    public bool CanDebit(decimal amount) => Type == LedgerAccountType.System || Balance >= amount;

    internal FundsDebited Debit(decimal amount, Guid? transferId, string description, DateTimeOffset now)
    {
        EnsurePositive(amount);

        if (!CanDebit(amount))
        {
            throw new DomainException("Saldo insuficiente.");
        }

        var debited = new FundsDebited(Guid.CreateVersion7(now), Id, amount, Balance - amount, transferId, description, now);
        Raise(debited);
        return debited;
    }

    internal FundsCredited Credit(decimal amount, Guid? transferId, string description, DateTimeOffset now)
    {
        EnsurePositive(amount);

        var credited = new FundsCredited(Guid.CreateVersion7(now), Id, amount, Balance + amount, transferId, description, now);
        Raise(credited);
        return credited;
    }

    private void Raise(object @event)
    {
        Apply(@event);
        _pendingEvents.Add(@event);
    }

    private void Apply(object @event)
    {
        switch (@event)
        {
            case LedgerAccountOpened opened:
                Id = opened.AccountId;
                Type = opened.Type;
                break;
            case FundsDebited debited:
                Balance = debited.BalanceAfter;
                break;
            case FundsCredited credited:
                Balance = credited.BalanceAfter;
                break;
            default:
                throw new InvalidOperationException($"Evento desconhecido: {@event.GetType().Name}");
        }
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("O valor do lançamento deve ser maior que zero.");
        }
    }
}
