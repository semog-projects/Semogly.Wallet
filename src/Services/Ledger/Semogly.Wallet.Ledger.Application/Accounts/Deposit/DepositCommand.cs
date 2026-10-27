using Semogly.Wallet.Contracts.Ledger;
using Semogly.Wallet.Ledger.Application.Abstractions;
using Semogly.Wallet.Ledger.Domain.Accounts;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Ledger.Application.Accounts.Deposit;

/// <summary>
/// Entrada de dinheiro externo (simula um PIX/boleto recebido), com contrapartida na conta CashIn.
/// Pedidos repetidos com a mesma Idempotency-Key devolvem o lançamento original.
/// </summary>
public sealed record DepositCommand(Guid AccountId, decimal Amount, string IdempotencyKey) : ICommand<DepositResponse>;

public sealed record DepositResponse(Guid EntryId, Guid AccountId, decimal Amount, decimal BalanceAfter, DateTimeOffset OccurredAt);

/// <summary>Registro do depósito efetivado para uma Idempotency-Key.</summary>
public sealed record DepositReceipt(
    Guid AccountId,
    string IdempotencyKey,
    Guid EntryId,
    decimal Amount,
    decimal BalanceAfter,
    DateTimeOffset OccurredAt)
{
    public DepositResponse ToResponse() => new(EntryId, AccountId, Amount, BalanceAfter, OccurredAt);
}

internal sealed class DepositCommandHandler(
    ILedgerAccountRepository accounts,
    IDepositReceiptStore receipts,
    IEventPublisher publisher,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DepositCommand, DepositResponse>
{
    public const string Description = "Depósito";
    public const int IdempotencyKeyMaxLength = 100;

    public static readonly Error InvalidIdempotencyKey = Error.Validation(
        "Deposit.InvalidIdempotencyKey",
        $"O header Idempotency-Key é obrigatório e deve ter até {IdempotencyKeyMaxLength} caracteres.");

    public static readonly Error IdempotencyKeyReused = Error.Conflict(
        "Deposit.IdempotencyKeyReused",
        "Esta Idempotency-Key já foi usada em um depósito com outro valor.");

    public async Task<Result<DepositResponse>> HandleAsync(DepositCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > IdempotencyKeyMaxLength)
        {
            return InvalidIdempotencyKey;
        }

        var existing = await receipts.FindAsync(command.AccountId, command.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            // Mesma chave e mesmo pedido: repetição (ex.: timeout do cliente), devolve o lançamento original
            return existing.Amount == command.Amount ? existing.ToResponse() : IdempotencyKeyReused;
        }

        var now = clock.GetUtcNow();
        var account = await accounts.GetCustomerAccountAsync(command.AccountId, cancellationToken);

        if (account is null)
        {
            return LedgerErrors.AccountNotFound(command.AccountId);
        }

        var cashIn = await accounts.GetOrOpenSystemAccountAsync(SystemAccounts.CashIn, now, cancellationToken);
        var (_, credit) = DoubleEntry.Post(cashIn, account, command.Amount, null, Description, now);
        var receipt = new DepositReceipt(
            account.Id, command.IdempotencyKey, credit.EntryId, credit.Amount, credit.BalanceAfter, now);

        await accounts.SaveAsync(cashIn, cancellationToken);
        await accounts.SaveAsync(account, cancellationToken);
        await receipts.AddAsync(receipt, cancellationToken);
        await publisher.PublishAsync(
            new AccountCredited(credit.EntryId, credit.AccountId, credit.Amount, null, now), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return receipt.ToResponse();
    }
}
