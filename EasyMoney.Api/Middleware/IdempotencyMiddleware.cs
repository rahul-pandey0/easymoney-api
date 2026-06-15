using System.Security.Claims;
using System.Text;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Middleware;

/// <summary>
/// Replays cached responses for POST requests that carry an Idempotency-Key header.
/// Stores the response body in idempotency_log for 24 hours.
/// Only active for authenticated POST requests.
/// </summary>
public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const string IdempotencyHeader = "Idempotency-Key";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public IdempotencyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, EasyMoneyDbContext db)
    {
        if (ctx.Request.Method != HttpMethods.Post
            || !ctx.Request.Headers.TryGetValue(IdempotencyHeader, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            await _next(ctx);
            return;
        }

        var rawKey = keyValues.First()!.Trim();
        if (rawKey.Length > 100) { await _next(ctx); return; }

        var tenantClaim = ctx.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantClaim) || !long.TryParse(tenantClaim, out var tenantId))
        {
            await _next(ctx);
            return;
        }

        var endpoint = ctx.Request.Path.ToString();
        var cutoff = DateTime.UtcNow.Subtract(Ttl);

        var existing = await db.IdempotencyLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == rawKey
                                      && x.TenantId == tenantId
                                      && x.Endpoint == endpoint
                                      && x.CreatedAt > cutoff);

        if (existing != null)
        {
            ctx.Response.StatusCode = existing.StatusCode ?? 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(existing.ResponseBody ?? "", Encoding.UTF8);
            return;
        }

        // Buffer the downstream response
        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;

        try
        {
            await _next(ctx);
        }
        finally
        {
            ctx.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var responseBody = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();

        if (ctx.Response.StatusCode is >= 200 and < 300)
        {
            try
            {
                db.IdempotencyLogs.Add(new IdempotencyLog
                {
                    IdempotencyKey = rawKey,
                    TenantId = tenantId,
                    Endpoint = endpoint,
                    StatusCode = ctx.Response.StatusCode,
                    ResponseBody = responseBody,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Non-fatal: log persistence failure, serve the response anyway
            }
        }

        await originalBody.WriteAsync(Encoding.UTF8.GetBytes(responseBody));
    }
}
