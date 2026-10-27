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
/// Timeouts: antifraude sem resposta → TransferFailed (nada foi lançado). Débito ou crédito sem resposta →
/// AwaitingCancellation: o Ledger decide, porque só ele sabe o que já foi lançado (ver CancelTransferPosting).
/// </remarks>
public sealed class TransferStateMachine : MassTransitStateMachine<TransferState>
{
    public const string RefundedReasonPrefix = "Estornada: ";
    public const string FraudCheckTimeoutReason = "O antifraude não respondeu a tempo.";
    public const string LedgerTimeoutReason = "O livro-razão não respondeu a tempo.";

    public State AwaitingFraudCheck { get; private set; } = null!;
    public State AwaitingDebit { get; private set; } = null!;
    public State AwaitingCredit { get; private set; } = null!;
    public State AwaitingRefund { get; private set; } = null!;
    public State AwaitingCancellation { get; private set; } = null!;

    public Event<TransferRequested> TransferRequested { get; private set; } = null!;
    public Event<TransferFraudCheckApproved> FraudCheckApproved { get; private set; } = null!;
    public Event<TransferFraudCheckRejected> FraudCheckRejected { get; private set; } = null!;
    public Event<AccountDebited> AccountDebited { get; private set; } = null!;
    public Event<AccountDebitRejected> DebitRejected { get; private set; } = null!;
    public Event<AccountCredited> AccountCredited { get; private set; } = null!;
    public Event<AccountCreditRejected> CreditRejected { get; private set; } = null!;
    public Event<TransferPostingCancelled> PostingCancelled { get; private set; } = null!;
    public Event<TransferPostingAlreadyCompleted> PostingAlreadyCompleted { get; private set; } = null!;

    public Schedule<TransferState, FraudCheckTimedOut> FraudCheckTimeout { get; private set; } = null!;
    public Schedule<TransferState, LedgerPostingTimedOut> LedgerTimeout { get; private set; } = null!;

    public TransferStateMachine(TimeProvider clock, TransferSagaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        InstanceState(saga => saga.CurrentState);

        Event(() => TransferRequested, e => e.CorrelateById(c => c.Message.TransferId));
        Event(() => FraudCheckApproved, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => FraudCheckRejected, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => DebitRejected, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => CreditRejected, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => PostingCancelled, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());
        Event(() => PostingAlreadyCompleted, e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing());

        // Lançamentos do Ledger sem TransferId (ex.: depósitos) não pertencem a nenhuma saga
        Event(() => AccountDebited, e => e.CorrelateById(c => c.Message.TransferId ?? Guid.Empty).DiscardWhenMissing());
        Event(() => AccountCredited, e => e.CorrelateById(c => c.Message.TransferId ?? Guid.Empty).DiscardWhenMissing());

        // Agendamentos persistidos (Quartz no Postgres): sobrevivem a reinícios do serviço
        Schedule(() => FraudCheckTimeout, saga => saga.FraudCheckTimeoutTokenId, schedule =>
        {
            schedule.Delay = options.FraudCheckTimeout;
            schedule.Received = e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing();
        });
        Schedule(() => LedgerTimeout, saga => saga.LedgerTimeoutTokenId, schedule =>
        {
            schedule.Delay = options.LedgerTimeout;
            schedule.Received = e => e.CorrelateById(c => c.Message.TransferId).DiscardWhenMissing();
        });

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
                .Schedule(FraudCheckTimeout, c => new FraudCheckTimedOut(c.Saga.CorrelationId))
                .TransitionTo(AwaitingFraudCheck));

        During(AwaitingFraudCheck,
            When(FraudCheckApproved)
                .Unschedule(FraudCheckTimeout)
                .Publish(c => new DebitAccount(c.Saga.CorrelationId, c.Saga.SourceAccountId, c.Saga.Amount))
                .Schedule(LedgerTimeout, c => new LedgerPostingTimedOut(c.Saga.CorrelationId))
                .TransitionTo(AwaitingDebit),
            When(FraudCheckRejected)
                .Unschedule(FraudCheckTimeout)
                .Publish(c => Failed(c.Saga, c.Message.Reason, clock))
                .Finalize(),

            // Nenhum lançamento foi pedido ao Ledger: falhar é seguro. Uma resposta tardia do antifraude é descartada
            When(FraudCheckTimeout.Received)
                .Publish(c => Failed(c.Saga, FraudCheckTimeoutReason, clock))
                .Finalize());

        During(AwaitingDebit,
            When(AccountDebited)
                .Publish(c => new CreditAccount(c.Saga.CorrelationId, c.Saga.DestinationAccountId, c.Saga.Amount))
                .Schedule(LedgerTimeout, c => new LedgerPostingTimedOut(c.Saga.CorrelationId))
                .TransitionTo(AwaitingCredit),
            When(DebitRejected)
                .Unschedule(LedgerTimeout)
                .Publish(c => Failed(c.Saga, c.Message.Reason, clock))
                .Finalize(),
            When(LedgerTimeout.Received)
                .Publish(c => new CancelTransferPosting(c.Saga.CorrelationId))
                .TransitionTo(AwaitingCancellation),
            Ignore(FraudCheckTimeout.Received));

        During(AwaitingCredit,
            When(AccountCredited)
                .Unschedule(LedgerTimeout)
                .Publish(c => Completed(c.Saga, clock))
                .Finalize(),

            // Compensação: o débito já foi efetivado, então devolve o valor à origem
            When(CreditRejected)
                .Unschedule(LedgerTimeout)
                .Then(c => c.Saga.FailureReason = c.Message.Reason)
                .Publish(c => new CreditAccount(c.Saga.CorrelationId, c.Saga.SourceAccountId, c.Saga.Amount))
                .TransitionTo(AwaitingRefund),

            // Estornar aqui por conta própria seria arriscado: se o crédito estiver só atrasado, o destino receberia
            // e a origem também. O Ledger cancela (crédito tardio não é mais lançado) e estorna, ou responde que já creditou
            When(LedgerTimeout.Received)
                .Publish(c => new CancelTransferPosting(c.Saga.CorrelationId))
                .TransitionTo(AwaitingCancellation),
            Ignore(FraudCheckTimeout.Received));

        // Uma recusa do estorno não é tratada: a mensagem vai para a fila de erro para intervenção manual
        During(AwaitingRefund,
            When(AccountCredited)
                .Publish(c => Failed(c.Saga, RefundedReasonPrefix + c.Saga.FailureReason, clock))
                .Finalize(),
            Ignore(LedgerTimeout.Received));

        // Desfecho decidido pelo Ledger; lançamentos e recusas atrasados que chegarem antes dele são ignorados
        During(AwaitingCancellation,
            When(PostingCancelled)
                .Publish(c => Failed(
                    c.Saga, c.Message.Refunded ? RefundedReasonPrefix + LedgerTimeoutReason : LedgerTimeoutReason, clock))
                .Finalize(),
            When(PostingAlreadyCompleted)
                .Publish(c => Completed(c.Saga, clock))
                .Finalize(),
            Ignore(AccountDebited),
            Ignore(AccountCredited),
            Ignore(DebitRejected),
            Ignore(CreditRejected),
            Ignore(LedgerTimeout.Received));

        // Instâncias finalizadas são removidas; o histórico fica no agregado Transfer
        SetCompletedWhenFinalized();
    }

    private static TransferCompleted Completed(TransferState saga, TimeProvider clock) => new(
        saga.CorrelationId,
        saga.SourceAccountId,
        saga.DestinationAccountId,
        saga.Amount,
        clock.GetUtcNow());

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
