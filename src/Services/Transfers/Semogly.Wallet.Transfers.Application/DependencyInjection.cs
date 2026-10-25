using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.SharedKernel.Messaging;
using Semogly.Wallet.Transfers.Application.Transfers;
using Semogly.Wallet.Transfers.Application.Transfers.CompleteTransfer;
using Semogly.Wallet.Transfers.Application.Transfers.CreateTransfer;
using Semogly.Wallet.Transfers.Application.Transfers.FailTransfer;
using Semogly.Wallet.Transfers.Application.Transfers.GetTransfer;

namespace Semogly.Wallet.Transfers.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<CreateTransferCommand, TransferResponse>, CreateTransferCommandHandler>();
        services.AddScoped<ICommandHandler<CompleteTransferCommand>, CompleteTransferCommandHandler>();
        services.AddScoped<ICommandHandler<FailTransferCommand>, FailTransferCommandHandler>();
        services.AddScoped<IQueryHandler<GetTransferQuery, TransferResponse>, GetTransferQueryHandler>();

        return services;
    }
}
