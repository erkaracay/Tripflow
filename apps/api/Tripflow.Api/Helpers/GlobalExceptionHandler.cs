using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Tripflow.Api.Helpers;

internal sealed class GlobalExceptionHandler(
    IHostEnvironment env,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Preserve the status code from framework-level bad-request exceptions
        // (malformed JSON body, null body where a shape was expected, etc.)
        // so callers still see a 400 instead of a generic 500.
        var statusCode = exception is BadHttpRequestException bhre
            ? bhre.StatusCode
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = statusCode;

        var details = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : "Bad request.",
            Instance = httpContext.Request.Path,
        };

        // correlationId is set by RequestLoggingMiddleware before this handler runs
        var correlationId = httpContext.TraceIdentifier;
        if (!string.IsNullOrEmpty(correlationId))
            details.Extensions["correlationId"] = correlationId;

        if (env.IsDevelopment())
            details.Detail = exception.ToString();

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = details,
        });
    }
}
