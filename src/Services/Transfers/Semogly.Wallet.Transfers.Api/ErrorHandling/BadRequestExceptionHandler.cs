using Microsoft.AspNetCore.Diagnostics;

namespace Semogly.Wallet.Transfers.Api.ErrorHandling;

/// <summary>
/// Corpo ou parâmetros ilegíveis (ex.: JSON inválido) viram 400 com ProblemDetails. Em Development o
/// minimal API lança <see cref="BadHttpRequestException"/>, que sem este handler terminaria em 500.
/// </summary>
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        httpContext.Response.StatusCode = badRequest.StatusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Requisição inválida",
                Detail = badRequest.Message,
                Status = badRequest.StatusCode,
            },
        });
    }
}
