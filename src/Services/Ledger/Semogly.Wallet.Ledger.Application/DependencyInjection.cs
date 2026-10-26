using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Ledger.Application.Accounts.Deposit;
using Semogly.Wallet.Ledger.Application.Accounts.GetBook;
using Semogly.Wallet.Ledger.Application.Accounts.GetStatement;
using Semogly.Wallet.Ledger.Application.Accounts.OpenAccount;
using Semogly.Wallet.Ledger.Application.Accounts.Transfers;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Ledger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<OpenLedgerAccountCommand>, OpenLedgerAccountCommandHandler>();
        services.AddScoped<ICommandHandler<DebitForTransferCommand>, DebitForTransferCommandHandler>();
        services.AddScoped<ICommandHandler<CreditForTransferCommand>, CreditForTransferCommandHandler>();
        services.AddScoped<ICommandHandler<DepositCommand, DepositResponse>, DepositCommandHandler>();
        services.AddScoped<IQueryHandler<GetStatementQuery, StatementResponse>, GetStatementQueryHandler>();
        services.AddScoped<IQueryHandler<GetBookQuery, LedgerBookResponse>, GetBookQueryHandler>();

        return services;
    }
}
