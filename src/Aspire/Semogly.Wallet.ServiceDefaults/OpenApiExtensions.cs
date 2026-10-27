using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Microsoft.Extensions.Hosting;

public static class OpenApiExtensions
{
    /// <summary>Header enviado pelo Gateway ao repassar o documento OpenAPI de um serviço.</summary>
    public const string GatewayPrefixHeader = "X-Gateway-Prefix";

    private const string BearerScheme = "Bearer";

    /// <summary>
    /// Quando o documento é lido através do Gateway, aponta o servidor para o prefixo do Gateway
    /// (ex.: <c>/api</c>) e declara a autenticação Bearer. Assim o "Try it" do Scalar agregado
    /// passa pelo Gateway, com JWT e rate limiting, em vez de chamar o serviço direto.
    /// </summary>
    public static OpenApiOptions AddGatewayServer(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddDocumentTransformer((document, context, _) =>
        {
            var httpContext = context.ApplicationServices.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var prefix = httpContext?.Request.Headers[GatewayPrefixHeader].ToString();

            if (string.IsNullOrEmpty(prefix))
            {
                return Task.CompletedTask;
            }

            // URL relativa: resolvida a partir do endereço do Gateway que serviu o documento
            document.Servers = [new OpenApiServer { Url = prefix }];

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Token do Keycloak (em desenvolvimento: POST /auth/token no Gateway com usuário e senha)",
            };
            document.Security =
            [
                new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] },
            ];

            return Task.CompletedTask;
        });

        return options;
    }
}
