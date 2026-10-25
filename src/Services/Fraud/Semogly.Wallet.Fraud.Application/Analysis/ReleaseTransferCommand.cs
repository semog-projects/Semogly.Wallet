using Semogly.Wallet.Fraud.Application.Abstractions;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Fraud.Application.Analysis;

/// <summary>Transferência falhou (ex.: saldo insuficiente): o valor volta para o limite diário da conta.</summary>
public sealed record ReleaseTransferCommand(Guid TransferId, Guid SourceAccountId, DateTimeOffset FailedAt) : ICommand;

internal sealed class ReleaseTransferCommandHandler(IAccountActivityStore activity) : ICommandHandler<ReleaseTransferCommand>
{
    public async Task<Result> HandleAsync(ReleaseTransferCommand command, CancellationToken cancellationToken)
    {
        await activity.ReleaseAsync(command.SourceAccountId, command.TransferId, command.FailedAt, cancellationToken);
        return Result.Success();
    }
}
