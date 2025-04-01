using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text.Json;

namespace ShelfieBackend.Middleware
{
    public class GlobalException
    {
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

                // Если ответ уже начат или является редиректом, ничего не делаем
                if (context.Response.HasStarted || (context.Response.StatusCode >= 300 && context.Response.StatusCode < 400))
                    return;

                int statusCode = context.Response.StatusCode;
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

                Log.Warning("HTTP Error: {StatusCode} - {Message}", statusCode, message);

                context.Response.Clear();
                context.Response.StatusCode = statusCode;
                await ModifyHeader(context, title, message, statusCode);
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
                    await ModifyHeader(context, title, message, statusCode);
                }
            }
        }

        private static async Task ModifyHeader(HttpContext context, string title, string message, int statusCode)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails()
            {
                Title = title,
                Detail = message,
                Status = statusCode
            }), CancellationToken.None);
        }
    }
}

