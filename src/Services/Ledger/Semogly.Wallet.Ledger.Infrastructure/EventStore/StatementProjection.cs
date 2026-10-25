using JasperFx.Events;
using Marten.Events.Projections;
using Semogly.Wallet.Ledger.Application.Accounts.GetStatement;
using Semogly.Wallet.Ledger.Domain.Accounts;

namespace Semogly.Wallet.Ledger.Infrastructure.EventStore;

/// <summary>Linha do extrato: documento de leitura gerado a partir dos eventos de lançamento.</summary>
public sealed class StatementEntry
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public EntryType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? TransferId { get; set; }
    public string Description { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Posição global do evento no event store, usada para ordenar o extrato.</summary>
    public long Sequence { get; set; }
}

/// <summary>Projeção inline: atualizada na mesma transação em que o lançamento é gravado.</summary>
public sealed partial class StatementProjection : EventProjection
{
    public static StatementEntry Create(IEvent<FundsDebited> @event) => new()
    {
        Id = @event.Data.EntryId,
        AccountId = @event.Data.AccountId,
        Type = EntryType.Debit,
        Amount = @event.Data.Amount,
        BalanceAfter = @event.Data.BalanceAfter,
        TransferId = @event.Data.TransferId,
        Description = @event.Data.Description,
        OccurredAt = @event.Data.OccurredAt,
        Sequence = @event.Sequence,
    };

    public static StatementEntry Create(IEvent<FundsCredited> @event) => new()
    {
        Id = @event.Data.EntryId,
        AccountId = @event.Data.AccountId,
        Type = EntryType.Credit,
        Amount = @event.Data.Amount,
        BalanceAfter = @event.Data.BalanceAfter,
        TransferId = @event.Data.TransferId,
        Description = @event.Data.Description,
        OccurredAt = @event.Data.OccurredAt,
        Sequence = @event.Sequence,
    };
}
