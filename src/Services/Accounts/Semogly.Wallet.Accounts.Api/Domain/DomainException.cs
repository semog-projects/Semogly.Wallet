namespace Semogly.Wallet.Accounts.Api.Domain;

/// <summary>Violação de regra de negócio. Mapeada para HTTP 422.</summary>
public sealed class DomainException(string message) : Exception(message);
