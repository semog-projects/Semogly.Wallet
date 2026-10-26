using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Domain.Accounts;
using Semogly.Wallet.Accounts.Infrastructure.Persistence;
using Semogly.Wallet.SharedKernel.Pagination;

namespace Semogly.Wallet.Accounts.Infrastructure.Caching;

/// <summary>Lado de leitura com cache em dois níveis (memória + Redis) via HybridCache.</summary>
internal sealed class CachedAccountReadService(HybridCache cache, AccountsDbContext db) : IAccountReadService
{
    public async Task<AccountResponse?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            AccountCacheKeys.Account(accountId),
            (db, accountId),
            // Projeta direto para o DTO, sem materializar a entidade de domínio
            static async (state, token) => await ToResponse(state.db.Accounts
                    .AsNoTracking()
                    .Where(a => a.Id == state.accountId))
                .FirstOrDefaultAsync(token),
            cancellationToken: cancellationToken);

    // Busca administrativa: resultado variável demais para valer a pena cachear
    public async Task<PagedResponse<AccountResponse>> SearchAsync(
        string? term, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Accounts.AsNoTracking();

        if (!string.IsNullOrEmpty(term))
        {
            if (LooksLikeDocument(term))
            {
                var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
                query = query.Where(a => a.Document.StartsWith(digits));
            }
            else
            {
                var pattern = $"%{EscapeLike(term)}%";
                query = query.Where(a => EF.Functions.ILike(a.OwnerName, pattern) || EF.Functions.ILike(a.Email, pattern));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await ToResponse(query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AccountResponse>(items, page, pageSize, total);
    }

    private static IQueryable<AccountResponse> ToResponse(IQueryable<Account> query) =>
        query.Select(a => new AccountResponse(a.Id, a.OwnerName, a.Email, a.Currency, a.Balance, a.Status, a.CreatedAt));

    // "123.456.789-09", "12345" etc.: só dígitos e a pontuação usual do CPF
    private static bool LooksLikeDocument(string term) =>
        term.Any(char.IsAsciiDigit) && term.All(c => char.IsAsciiDigit(c) || c is '.' or '-' or ' ');

    // O termo é tratado como texto literal: % e _ digitados não viram curingas
    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
