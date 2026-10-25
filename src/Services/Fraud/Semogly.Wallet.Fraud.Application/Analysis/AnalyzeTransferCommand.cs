using Semogly.Wallet.Contracts.Fraud;
using Semogly.Wallet.Fraud.Application.Abstractions;
using Semogly.Wallet.Fraud.Domain.Analysis;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Fraud.Application.Analysis;

/// <summary>Avalia uma transferência pedida pela saga e responde com aprovação ou recusa.</summary>
public sealed record AnalyzeTransferCommand(Guid TransferId, Guid SourceAccountId, decimal Amount) : ICommand;

internal sealed class AnalyzeTransferCommandHandler(
    FraudPolicy policy,
    IAccountActivityStore activity,
    IEventPublisher publisher,
    TimeProvider clock) : ICommandHandler<AnalyzeTransferCommand>
{
    public async Task<Result> HandleAsync(AnalyzeTransferCommand command, CancellationToken cancellationToken)
    {
        // Regras sem estado primeiro: recusas aqui não consomem limite diário nem velocity
        var rejection = TransactionLimitRule.Evaluate(command.Amount, policy)
            ?? await activity.TryRegisterAsync(
                command.SourceAccountId, command.TransferId, command.Amount, clock.GetUtcNow(), cancellationToken);

        if (rejection is { } reason)
        {
            await publisher.PublishAsync(new TransferFraudCheckRejected(command.TransferId, reason.ToReason()), cancellationToken);
        }
        else
        {
            await publisher.PublishAsync(new TransferFraudCheckApproved(command.TransferId), cancellationToken);
        }

        return Result.Success();
    }
}
