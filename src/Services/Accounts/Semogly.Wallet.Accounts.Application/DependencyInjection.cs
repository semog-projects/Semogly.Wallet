using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Application.Accounts.ApplyLedgerEntry;
using Semogly.Wallet.Accounts.Application.Accounts.CreateAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetBalance;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Accounts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<CreateAccountCommand, AccountResponse>, CreateAccountCommandHandler>();
        services.AddScoped<ICommandHandler<ApplyLedgerEntryCommand>, ApplyLedgerEntryCommandHandler>();
        services.AddScoped<IQueryHandler<GetAccountQuery, AccountResponse>, GetAccountQueryHandler>();
        services.AddScoped<IQueryHandler<GetBalanceQuery, BalanceResponse>, GetBalanceQueryHandler>();

        return services;
    }
}
