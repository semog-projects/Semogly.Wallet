namespace Semogly.Wallet.Accounts.Application.Accounts;

/// <summary>
/// Lado de leitura (CQRS): consulta projeções prontas para resposta,
/// sem passar pelo modelo de domínio.
/// </summary>
public interface IAccountReadService
{
    Task<AccountResponse?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken);
}
