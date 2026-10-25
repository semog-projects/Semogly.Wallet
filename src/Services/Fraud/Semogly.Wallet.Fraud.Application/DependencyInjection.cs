using Microsoft.Extensions.DependencyInjection;
using Semogly.Wallet.Fraud.Application.Analysis;
using Semogly.Wallet.SharedKernel.Messaging;

namespace Semogly.Wallet.Fraud.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<AnalyzeTransferCommand>, AnalyzeTransferCommandHandler>();
        services.AddScoped<ICommandHandler<ReleaseTransferCommand>, ReleaseTransferCommandHandler>();
        services.AddScoped<IQueryHandler<GetDailyUsageQuery, DailyUsageResponse>, GetDailyUsageQueryHandler>();

        return services;
    }
}
