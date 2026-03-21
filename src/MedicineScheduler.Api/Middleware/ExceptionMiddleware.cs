// src/MedicineScheduler.Api/Middleware/ExceptionMiddleware.cs
using System.Text.Json;

namespace MedicineScheduler.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException)
        {
            context.Response.StatusCode = 404;
            await WriteJson(context, new { error = "Not found." });
        }
        catch (UnauthorizedAccessException)
        {
            context.Response.StatusCode = 403;
            await WriteJson(context, new { error = "Access denied." });
        }
        catch (InvalidOperationException ex)
        {
            context.Response.StatusCode = 409;
            await WriteJson(context, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await WriteJson(context, new { error = "An unexpected error occurred." });
        }
    }

    private static Task WriteJson(HttpContext ctx, object body)
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
