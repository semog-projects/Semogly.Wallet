using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Transfers.Application.Transfers.CreateTransfer;

public sealed record CreateTransferCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string IdempotencyKey) : ICommand<TransferResponse>;
