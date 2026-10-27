using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Semogly.Wallet.Gateway.Bff;
using Semogly.Wallet.Gateway.RateLimiting;
using Semogly.Wallet.Gateway.Realtime;
using Semogly.Wallet.Gateway.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Rotas e clusters em appsettings.json; endereços resolvidos pelo service discovery do Aspire
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddBffTokenForwarding();

builder.Services.AddSingleton(TimeProvider.System);
builder.AddGatewaySecurity();
builder.Services.AddGatewayRateLimiting();
builder.Services.AddProblemDetails();
builder.AddBffDataProtection();
builder.AddBffSessionStore();
builder.AddRealtime();

// O front (proxy do Vite em dev, ingress em produção) informa a origem pública: o redirect_uri do login OIDC
// precisa apontar para ela. Por padrão só proxies locais (loopback) são confiáveis.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Autenticação antes do rate limiter: a partição do limite é o usuário (do token ou da sessão)
app.UseAuthentication();
app.UseBffCsrfProtection();
app.UseRateLimiter();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapBffEndpoints();

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

app.MapHub<TransfersHub>(TransfersHub.Route);
app.MapReverseProxy();

await app.RunAsync();
