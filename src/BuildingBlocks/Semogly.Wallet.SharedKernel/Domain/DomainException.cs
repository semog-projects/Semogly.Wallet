namespace Semogly.Wallet.SharedKernel.Domain;

/// <summary>Violação de invariante do domínio. Mapeada para HTTP 422 na camada de API.</summary>
public class DomainException(string message) : Exception(message);
