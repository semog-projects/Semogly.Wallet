using Semogly.Wallet.SharedKernel.Domain;

namespace Semogly.Wallet.Ledger.Domain.Transfers;

/// <summary>
/// Lançamentos de uma transferência no livro-razão (débito, crédito, estorno e cancelamento), com Event Sourcing.
/// É o árbitro dos timeouts da saga: depois do cancelamento, débito e crédito atrasados não são mais lançados,
/// e um débito já lançado é estornado. Assim, a origem nunca é estornada se o destino já recebeu.
/// </summary>
public sealed class TransferPosting
{
    private readonly List<object> _pendingEvents = [];

    private TransferPosting() { }

    /// <summary>Igual ao Id da transferência.</summary>
    public Guid Id { get; private set; }

    public Guid? SourceAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public bool IsDebited { get; private set; }
    public bool IsCredited { get; private set; }
    public bool IsRefunded { get; private set; }
    public bool IsCancelled { get; private set; }

    /// <summary>Quantidade de eventos já gravados no stream (usada na concorrência otimista).</summary>
    public long PersistedVersion { get; private set; }

    public IReadOnlyList<object> PendingEvents => _pendingEvents;

    /// <summary>Transferência ainda sem lançamentos.</summary>
    public static TransferPosting Start(Guid transferId) => new() { Id = transferId };

    /// <summary>Reconstrói os lançamentos da transferência a partir dos eventos gravados.</summary>
    public static TransferPosting Rehydrate(Guid transferId, IEnumerable<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var posting = Start(transferId);

        foreach (var @event in events)
        {
            posting.Apply(@event);
            posting.PersistedVersion++;
        }

        return posting;
    }

    /// <summary>Débito ainda pode ser lançado: nem cancelado, nem já lançado (mensagem duplicada).</summary>
    public bool CanDebit => !IsCancelled && !IsDebited;

    /// <summary>Crédito no destino ainda pode ser lançado.</summary>
    public bool CanCredit => !IsCancelled && !IsCredited;

    /// <summary>Débito lançado e ainda não devolvido à origem.</summary>
    public bool NeedsRefund => IsDebited && !IsCredited && !IsRefunded;

    /// <summary>O crédito pedido para a origem de um débito já lançado é o estorno da saga.</summary>
    public bool IsRefundTo(Guid accountId) => IsDebited && accountId == SourceAccountId;

    public void RecordDebit(Guid sourceAccountId, decimal amount, DateTimeOffset now)
    {
        Ensure(CanDebit, "O débito da transferência não pode ser lançado.");
        Raise(new TransferDebitRecorded(Id, sourceAccountId, amount, now));
    }

    public void RecordCredit(Guid destinationAccountId, DateTimeOffset now)
    {
        Ensure(CanCredit && IsDebited, "O crédito da transferência não pode ser lançado.");
        Raise(new TransferCreditRecorded(Id, destinationAccountId, now));
    }

    public void RecordRefund(DateTimeOffset now)
    {
        Ensure(NeedsRefund, "Não há débito a estornar.");
        Raise(new TransferRefundRecorded(Id, now));
    }

    /// <summary>Cancela por timeout da saga. Não é possível depois do crédito: a transferência já se completou.</summary>
    public void Cancel(DateTimeOffset now)
    {
        Ensure(!IsCredited, "A transferência já foi creditada no destino.");

        if (!IsCancelled)
        {
            Raise(new TransferCancelled(Id, now));
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new DomainException(message);
        }
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
            case TransferDebitRecorded debited:
                SourceAccountId = debited.SourceAccountId;
                Amount = debited.Amount;
                IsDebited = true;
                break;
            case TransferCreditRecorded:
                IsCredited = true;
                break;
            case TransferRefundRecorded:
                IsRefunded = true;
                break;
            case TransferCancelled:
                IsCancelled = true;
                break;
            default:
                throw new InvalidOperationException($"Evento desconhecido: {@event.GetType().Name}");
        }
    }
}
