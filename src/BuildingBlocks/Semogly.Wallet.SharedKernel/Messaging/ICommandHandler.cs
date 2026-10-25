using Semogly.Wallet.SharedKernel.Results;

namespace Semogly.Wallet.SharedKernel.Messaging;

/// <summary>Marcador de um comando (operação que altera estado).</summary>
public interface ICommand;

/// <summary>Marcador de um comando que retorna um valor.</summary>
public interface ICommand<TResponse>;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
