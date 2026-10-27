using JasperFx;
using Microsoft.AspNetCore.Diagnostics;

namespace Semogly.Wallet.Ledger.Api.ErrorHandling;

/// <summary>
/// Conflito de concorrência no event store (stream alterado por outro pedido, ou recibo de depósito já gravado
/// por um pedido simultâneo com a mesma Idempotency-Key): HTTP 409, o cliente pode repetir o pedido.
/// </summary>
internal sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (ConcurrencyException or DocumentAlreadyExistsException)
            && exception.InnerException is not (ConcurrencyException or DocumentAlreadyExistsException))
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Conflito de concorrência",
                Detail = "Outro pedido alterou os mesmos dados ao mesmo tempo. Repita o pedido.",
                Status = StatusCodes.Status409Conflict,
            },
        });
    }
}
