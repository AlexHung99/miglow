using GongWei.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GongWei.Api.Http;

/// <summary>
/// Turns domain rule violations into Problem Details with a stable <c>code</c> (spec §7.2).
/// Anything that is not a <see cref="DomainException"/> is a bug, and becomes a bare 500 —
/// no stack trace, SQL or connection string ever reaches the client (spec §11).
/// </summary>
public sealed class DomainExceptionHandler(
    ILogger<DomainExceptionHandler> logger,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, problem) = exception switch
        {
            DomainException domain => (StatusFor(domain.Kind), Build(domain)),
            OperationCanceledException => (499, new ProblemDetails
            {
                Title = "Client closed request",
                Detail = "The request was cancelled."
            }),
            _ => (StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal server error",
                Detail = "Something went wrong. Quote the request id when reporting this."
            })
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("{Status} on {Method} {Path}: {Message}",
                status, httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }

        problem.Status = status;
        problem.Extensions["requestId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails Build(DomainException domain)
    {
        var problem = new ProblemDetails
        {
            Title = domain.Code,
            Detail = domain.Message
        };

        problem.Extensions["code"] = domain.Code;

        foreach (var (key, value) in domain.Extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }

    private static int StatusFor(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Validation => StatusCodes.Status400BadRequest,
        DomainErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
        DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
        DomainErrorKind.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
        DomainErrorKind.UnsupportedMedia => StatusCodes.Status415UnsupportedMediaType,
        DomainErrorKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
        DomainErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        DomainErrorKind.Maintenance => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status409Conflict
    };
}
