using ApiForge.Shared.Responses;

namespace ApiForge.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(Result<object>.Failure(
                "An unexpected error occurred.",
                new ErrorDetail("server.error", "An unexpected error occurred.")));
        }
    }
}
