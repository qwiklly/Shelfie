using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text.Json;

namespace ShelfieBackend.Middleware
{
    /// <summary>
    /// Turns unhandled exceptions and empty error responses (401/403/404/... without a body) into JSON ProblemDetails.
    /// Successful responses and error responses that already have a body are left untouched.
    /// </summary>
    public class GlobalException
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly RequestDelegate _next;

        public GlobalException(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);

                int statusCode = context.Response.StatusCode;
                if (context.Response.HasStarted || statusCode < 400 || !string.IsNullOrEmpty(context.Response.ContentType))
                    return;

                string title = statusCode switch
                {
                    StatusCodes.Status429TooManyRequests => "Warning",
                    StatusCodes.Status401Unauthorized => "Warning",
                    StatusCodes.Status403Forbidden => "Warning",
                    StatusCodes.Status404NotFound => "Not Found",
                    _ => "Error"
                };

                string message = statusCode switch
                {
                    StatusCodes.Status429TooManyRequests => "Too many requests made",
                    StatusCodes.Status401Unauthorized => "You aren't authorized",
                    StatusCodes.Status403Forbidden => "You are not allowed to access",
                    StatusCodes.Status404NotFound => "Resource not found",
                    _ => "Server error occurred"
                };

                Log.Warning("HTTP Error: {StatusCode} - {Message} ({Path})", statusCode, message, context.Request.Path);

                // Headers such as WWW-Authenticate are kept: only the body is filled in.
                await WriteProblemAsync(context, title, message, statusCode);
            }
            catch (Exception ex) when (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                // The client went away — nothing to answer.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception");

                int statusCode = ex switch
                {
                    TaskCanceledException or TimeoutException => StatusCodes.Status408RequestTimeout,
                    HttpRequestException => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status500InternalServerError
                };

                string title = statusCode switch
                {
                    StatusCodes.Status408RequestTimeout => "Out of Time",
                    StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
                    _ => "Error"
                };

                string message = statusCode switch
                {
                    StatusCodes.Status408RequestTimeout => "Request timeout, please try again",
                    StatusCodes.Status503ServiceUnavailable => "Service is temporarily unavailable, please try again",
                    _ => "Server error occurred"
                };

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = statusCode;
                    await WriteProblemAsync(context, title, message, statusCode);
                }
            }
        }

        private static async Task WriteProblemAsync(HttpContext context, string title, string message, int statusCode)
        {
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
            {
                Title = title,
                Detail = message,
                Status = statusCode
            }, JsonOptions), CancellationToken.None);
        }
    }
}
