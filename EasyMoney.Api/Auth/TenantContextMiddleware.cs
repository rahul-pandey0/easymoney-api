using System.Security.Claims;

namespace EasyMoney.Api.Auth;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    //public async Task Invoke(HttpContext ctx, ITenantContext tenantCtx)
    //{
    //    if (ctx.User?.Identity?.IsAuthenticated == true && tenantCtx is TenantContext tc)
    //    {
    //        var tenantClaim = ctx.User.FindFirst("tenant_id")?.Value;

    //        var userClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //        var roleClaim = ctx.User.FindFirst(ClaimTypes.Role)?.Value;

    //        if (long.TryParse(tenantClaim, out var tId)) tc.TenantId = tId;
    //        if (long.TryParse(userClaim, out var uId)) tc.UserId = uId;
    //        tc.Role = roleClaim;

    //        // Enable tenant bypass for SIFIN users
    //        if (tc.IsSifin)
    //        {
    //            tc.SetBypass(true);
    //        }
    //    }
    //    await _next(ctx);
    //}
    public async Task Invoke(HttpContext ctx, ITenantContext tenantCtx)
    {
        if (ctx.User?.Identity?.IsAuthenticated == true && tenantCtx is TenantContext tc)
        {
            var tenantClaim = ctx.User.FindFirst("tenant_id")?.Value;
            var userClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = ctx.User.FindFirst(ClaimTypes.Role)?.Value;

            // Debug
            Console.WriteLine($"Authenticated = {ctx.User.Identity?.IsAuthenticated}");
            Console.WriteLine($"tenant_id claim = {tenantClaim}");
            Console.WriteLine($"user_id = {userClaim}");
            Console.WriteLine($"role = {roleClaim}");

            if (long.TryParse(tenantClaim, out var tId))
                tc.TenantId = tId;

            if (long.TryParse(userClaim, out var uId))
                tc.UserId = uId;

            tc.Role = roleClaim;

            Console.WriteLine($"TenantContext TenantId = {tc.TenantId}");

            if (tc.IsSifin)
            {
                tc.SetBypass(true);
            }
        }

        await _next(ctx);
    }
}
