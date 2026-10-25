using MassTransit;
using Semogly.Wallet.Contracts.Fraud;
using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Contracts.Transfers;

namespace Semogly.Wallet.Transfers.Infrastructure.Sagas;

/// <summary>
/// Orquestra a transferência: antifraude → débito na origem → crédito no destino.
/// Se o crédito for recusado, a saga compensa estornando (creditando de volta) a origem.
/// </summary>
/// <remarks>
/// Fluxo feliz: TransferRequested → AwaitingFraudCheck → AwaitingDebit → AwaitingCredit → TransferCompleted.
/// Compensação: AwaitingCredit → (crédito recusado) → AwaitingRefund → TransferFailed.
/// </remarks>
public sealed class TransferStateMachine : MassTransitStateMachine<TransferState>
{
    public const string RefundedReasonPrefix = "Estornada: ";

    public State AwaitingFraudCheck { get; private set; } = null!;
    public State AwaitingDebit { get; private set; } = null!;
    public State AwaitingCredit { get; private set; } = null!;
    public State AwaitingRefund { get; private set; } = null!;

    public Event<TransferRequested> TransferRequested { get; private set; } = null!;
    public Event<TransferFraudCheckApproved> FraudCheckApproved { get; private set; } = null!;
    public Event<TransferFraudCheckRejected> FraudCheckRejected { get; private set; } = null!;
    public Event<AccountDebited> AccountDebited { get; private set; } = null!;
    public Event<AccountDebitRejected> DebitRejected { get; private set; } = null!;
    public Event<AccountCredited> AccountCredited { get; private set; } = null!;
    public Event<AccountCreditRejected> CreditRejected { get; private set; } = null!;

    public TransferStateMachine(TimeProvider clock)
    {
        InstanceState(saga => saga.CurrentState);

        Event(() => TransferRequested, e => e.CorrelateById(c => c.Message.TransferId));
        Event(() => FraudCheckApproved, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => FraudCheckRejected, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => DebitRejected, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => CreditRejected, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());

        // Lançamentos do Ledger sem TransferId (ex.: depósitos) não pertencem a nenhuma saga
        Event(() => AccountDebited, e => e.CorrelateById(c => c.Message.TransferId ?? Guid.Empty).DiscardWhenMissing());
        Event(() => AccountCredited, e => e.CorrelateById(c => c.Message.TransferId ?? Guid.Empty).DiscardWhenMissing());

        Initially(
            When(TransferRequested)
                .Then(c =>
                {
                    c.Saga.SourceAccountId = c.Message.SourceAccountId;
                    c.Saga.DestinationAccountId = c.Message.DestinationAccountId;
                    c.Saga.Amount = c.Message.Amount;
                    c.Saga.CreatedAt = c.Message.RequestedAt;
                })
                .Publish(c => new CheckTransferFraud(
                    c.Saga.CorrelationId, c.Saga.SourceAccountId, c.Saga.DestinationAccountId, c.Saga.Amount))
                .TransitionTo(AwaitingFraudCheck));

        During(AwaitingFraudCheck,
            When(FraudCheckApproved)
                .Publish(c => new DebitAccount(c.Saga.CorrelationId, c.Saga.SourceAccountId, c.Saga.Amount))
                .TransitionTo(AwaitingDebit),
            When(FraudCheckRejected)
                .Publish(c => Failed(c.Saga, c.Message.Reason, clock))
                .Finalize());

        During(AwaitingDebit,
            When(AccountDebited)
                .Publish(c => new CreditAccount(c.Saga.CorrelationId, c.Saga.DestinationAccountId, c.Saga.Amount))
                .TransitionTo(AwaitingCredit),
            When(DebitRejected)
                .Publish(c => Failed(c.Saga, c.Message.Reason, clock))
                .Finalize());

        During(AwaitingCredit,
            When(AccountCredited)
                .Publish(c => new TransferCompleted(
                    c.Saga.CorrelationId, c.Saga.SourceAccountId, c.Saga.DestinationAccountId, c.Saga.Amount, clock.GetUtcNow()))
                .Finalize(),

            // Compensação: o débito já foi efetivado, então devolve o valor à origem
            When(CreditRejected)
                .Then(c => c.Saga.FailureReason = c.Message.Reason)
                .Publish(c => new CreditAccount(c.Saga.CorrelationId, c.Saga.SourceAccountId, c.Saga.Amount))
                .TransitionTo(AwaitingRefund));

        // Uma recusa do estorno não é tratada: a mensagem vai para a fila de erro para intervenção manual
        During(AwaitingRefund,
            When(AccountCredited)
                .Publish(c => Failed(c.Saga, RefundedReasonPrefix + c.Saga.FailureReason, clock))
                .Finalize());

        // Instâncias finalizadas são removidas; o histórico fica no agregado Transfer
        SetCompletedWhenFinalized();
    }

    private static TransferFailed Failed(TransferState saga, string reason, TimeProvider clock) => new(
        saga.CorrelationId,
        saga.SourceAccountId,
        saga.DestinationAccountId,
        saga.Amount,
        reason,
        clock.GetUtcNow());
}

file static class EventCorrelationExtensions
{
    /// <summary>Mensagens duplicadas ou tardias de sagas já finalizadas são descartadas.</summary>
    public static void DiscardWhenMissing<TMessage>(this IEventCorrelationConfigurator<TransferState, TMessage> correlation)
        where TMessage : class =>
        correlation.OnMissingInstance(m => m.Discard());
}
