using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Accounts.Application.Accounts;
using Semogly.Wallet.Accounts.Application.Accounts.ApplyLedgerEntry;
using Semogly.Wallet.Accounts.Application.Accounts.CreateAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetAccount;
using Semogly.Wallet.Accounts.Application.Accounts.GetBalance;
using Semogly.Wallet.Accounts.Application.Accounts.GetMyAccounts;
using Semogly.Wallet.Accounts.Application.Accounts.LookupAccount;
using Semogly.Wallet.Accounts.Application.Accounts.SearchAccounts;
using Semogly.Wallet.SharedKernel.Pagination;
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
        services.AddScoped<IQueryHandler<GetMyAccountsQuery, IReadOnlyList<AccountResponse>>, GetMyAccountsQueryHandler>();
        services.AddScoped<IQueryHandler<LookupAccountQuery, AccountLookupResponse>, LookupAccountQueryHandler>();
        services.AddScoped<IQueryHandler<GetBalanceQuery, BalanceResponse>, GetBalanceQueryHandler>();
        services.AddScoped<IQueryHandler<SearchAccountsQuery, PagedResponse<AccountResponse>>, SearchAccountsQueryHandler>();

        return services;
    }
}
