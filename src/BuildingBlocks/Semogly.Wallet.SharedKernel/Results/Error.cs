using System.Diagnostics.CodeAnalysis;

namespace Semogly.Wallet.SharedKernel.Results;

/// <summary>Erro esperado de um caso de uso (não excepcional).</summary>
[SuppressMessage("Naming", "CA1716", Justification = "Solution exclusivamente em C#; 'Error' só é palavra reservada em VB.")]
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}
