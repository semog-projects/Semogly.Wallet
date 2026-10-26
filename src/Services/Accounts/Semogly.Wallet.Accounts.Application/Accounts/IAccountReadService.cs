using Semogly.Wallet.SharedKernel.Pagination;

namespace Semogly.Wallet.Accounts.Application.Accounts;

/// <summary>
/// Lado de leitura (CQRS): consulta projeções prontas para resposta,
/// sem passar pelo modelo de domínio.
/// </summary>
public interface IAccountReadService
{
    Task<AccountResponse?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Busca por trecho do nome ou do e-mail, ou pelo início do CPF (sem cache).</summary>
    Task<PagedResponse<AccountResponse>> SearchAsync(string? term, int page, int pageSize, CancellationToken cancellationToken);
}
