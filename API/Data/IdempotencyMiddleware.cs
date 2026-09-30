using API.Data;
using API.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db)
    {
        // Only protect requests that explicitly require idempotency

        if (!HttpMethods.IsPost(context.Request.Method) ||
    !context.Request.Path.Equals("/payments/send", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }
        var idempotencyKey =
            context.Request.Headers["Idempotency-Key"]
                .FirstOrDefault();

        // No key = normal request
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            await context.Response.WriteAsJsonAsync(new
            {
                message =
                    "Idempotency-Key header is required."
            });

            return;
        }
       
        var userId =
            context.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            await context.Response.WriteAsJsonAsync(new
            {
                message = "Authentication is required."
            });

            return;
        }

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        var existingRecord =
            await db.IdempotencyRecords
                .FirstOrDefaultAsync(x =>
                    x.Key == idempotencyKey &&
                    x.UserId == userId &&
                    x.Method == method &&
                    x.Path == path);

        // =========================================================
        // REQUEST ALREADY EXISTS
        // =========================================================

        if (existingRecord != null)
        {
            // Previous request already completed
            if (existingRecord.Status == "Completed")
            {
                context.Response.StatusCode =
                    existingRecord.ResponseStatusCode ?? 200;

                if (!string.IsNullOrWhiteSpace(
                    existingRecord.ContentType))
                {
                    context.Response.ContentType =
                        existingRecord.ContentType;
                }

                if (!string.IsNullOrWhiteSpace(
                    existingRecord.ResponseBody))
                {
                    await context.Response.WriteAsync(
                        existingRecord.ResponseBody);
                }

                return;
            }

            // Another request is currently processing
            context.Response.StatusCode =
                StatusCodes.Status409Conflict;

            await context.Response.WriteAsJsonAsync(new
            {
                message =
                    "This request is already being processed."
            });

            return;
        }

        // =========================================================
        // CREATE IDEMPOTENCY RECORD
        // =========================================================

        var record = new IdempotencyRecord
        {
            Key = idempotencyKey,
            UserId = userId,
            Method = method,
            Path = path,
            Status = "Processing",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        db.IdempotencyRecords.Add(record);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another identical request won the race
            context.Response.StatusCode =
                StatusCodes.Status409Conflict;

            await context.Response.WriteAsJsonAsync(new
            {
                message =
                    "This request is already being processed."
            });

            return;
        }

        // =========================================================
        // CAPTURE RESPONSE
        // =========================================================

        var originalBody = context.Response.Body;

        await using var memoryStream =
            new MemoryStream();

        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Position = 0;

            using var reader =
                new StreamReader(memoryStream);

            var responseBody =
                await reader.ReadToEndAsync();

            record.Status = "Completed";
            record.ResponseStatusCode =
                context.Response.StatusCode;

            record.ResponseBody = responseBody;
            record.ContentType =
                context.Response.ContentType;

            await db.SaveChangesAsync();

            memoryStream.Position = 0;

            await memoryStream.CopyToAsync(
                originalBody);
        }
        catch
        {
            // Remove the idempotency record if processing failed
            db.IdempotencyRecords.Remove(record);

            await db.SaveChangesAsync();

            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}