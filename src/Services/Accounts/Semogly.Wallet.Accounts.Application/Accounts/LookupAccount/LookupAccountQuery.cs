using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.Accounts.Application.Accounts.LookupAccount;

/// <summary>Dados mínimos do destinatário para o pagador confirmar a transferência.</summary>
public sealed record AccountLookupResponse(Guid AccountId, string DisplayName);

/// <summary>Busca a conta de destino pelo e-mail do titular, como uma chave PIX.</summary>
public sealed record LookupAccountQuery(string Email) : IQuery<AccountLookupResponse>;

internal sealed class LookupAccountQueryHandler(IAccountReadService readService)
    : IQueryHandler<LookupAccountQuery, AccountLookupResponse>
{
    public static readonly Error NotFound =
        Error.NotFound("Account.LookupNotFound", "Nenhuma conta ativa com este e-mail.");

    public async Task<Result<AccountLookupResponse>> HandleAsync(LookupAccountQuery query, CancellationToken cancellationToken)
    {
        var accounts = await readService.GetByOwnerEmailAsync(query.Email.Trim().ToLowerInvariant(), cancellationToken);
        var account = accounts.FirstOrDefault(a => a.Status == Domain.Accounts.AccountStatus.Active);

        return account is null ? NotFound : new AccountLookupResponse(account.Id, MaskName(account.OwnerName));
    }

    /// <summary>"Maria Souza Lima" → "Maria L.": o suficiente para confirmar, sem expor o nome completo.</summary>
    internal static string MaskName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}." : parts[0];
    }
}
