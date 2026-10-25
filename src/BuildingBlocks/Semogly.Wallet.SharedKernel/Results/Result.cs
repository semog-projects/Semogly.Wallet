using System.Diagnostics.CodeAnalysis;

namespace Semogly.Wallet.SharedKernel.Results;

/// <summary>
/// Resultado de um caso de uso. Falhas esperadas (não encontrado, conflito...) são
/// retornadas como <see cref="Error"/> em vez de lançadas como exceção.
/// </summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, null);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, Error? error)
        : base(error) => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado com falha.");

    public static implicit operator Result<TValue>(TValue value) => new(value, null);

    public static implicit operator Result<TValue>(Error error) => new(default, error);
}
