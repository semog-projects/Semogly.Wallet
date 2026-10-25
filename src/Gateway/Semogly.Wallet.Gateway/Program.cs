using Scalar.AspNetCore;
using Semogly.Wallet.Gateway.RateLimiting;
using Semogly.Wallet.Gateway.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Rotas e clusters em appsettings.json; endereços resolvidos pelo service discovery do Aspire
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddGatewaySecurity(builder.Configuration);
builder.Services.AddGatewayRateLimiting();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Autenticação antes do rate limiter: a partição do limite é o usuário do token
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapDevelopmentTokenEndpoint();

    // Documentos OpenAPI dos serviços, repassados pelo próprio Gateway (rotas openapi-* do YARP)
    app.MapScalarApiReference(options => options
        .WithTitle("Semogly Wallet API")
        .AddDocument("accounts", "Accounts", "/openapi/accounts.json", isDefault: true)
        .AddDocument("transfers", "Transfers", "/openapi/transfers.json")
        .AddDocument("ledger", "Ledger", "/openapi/ledger.json")
        .AddDocument("fraud", "Fraud", "/openapi/fraud.json")
        .AddDocument("notifications", "Notifications", "/openapi/notifications.json"));
}

app.MapReverseProxy();

await app.RunAsync();
