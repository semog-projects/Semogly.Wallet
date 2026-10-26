namespace Semogly.Wallet.SharedKernel.Pagination;

/// <summary>Página de resultados com o total, para a navegação no cliente.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
