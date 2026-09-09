using EasyMoney.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EasyMoney.Api.Auth;

public class TenantContextMiddleware
{ 
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    public TenantContextMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _scopeFactory = scopeFactory;
    }

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
    //public async Task Invoke(HttpContext ctx, ITenantContext tenantCtx)
    //{
    //    if (ctx.User?.Identity?.IsAuthenticated == true && tenantCtx is TenantContext tc)
    //    {
    //        var tenantClaim = ctx.User.FindFirst("tenant_id")?.Value;
    //        var userClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //        var roleClaim = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
    //        var branchClaim = ctx.User.FindFirst("branch_id")?.Value;

    //        // Debug
    //        Console.WriteLine($"Authenticated = {ctx.User.Identity?.IsAuthenticated}");
    //        Console.WriteLine($"tenant_id claim = {tenantClaim}");
    //        Console.WriteLine($"user_id = {userClaim}");
    //        Console.WriteLine($"role = {roleClaim}");

    //        if (long.TryParse(tenantClaim, out var tId))
    //            tc.TenantId = tId;

    //        if (long.TryParse(userClaim, out var uId))
    //            tc.UserId = uId;

    //        tc.Role = roleClaim;
    //        tc.BranchName = branchClaim;


    //        if (long.TryParse(branchClaim, out var bId))
    //            tc.BranchId = bId;

    //       Console.WriteLine($"TenantContext TenantId = {tc.TenantId}");

    //        if (tc.IsSifin)
    //        {
    //            tc.SetBypass(true);
    //        }

    //        //if (long.TryParse(branchClaim, out var bId))
    //        //    tc.BranchId = bId;  
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
            var branchClaim = ctx.User.FindFirst("branch_id")?.Value;
            var branchNameClaim = ctx.User.FindFirst("branch_name")?.Value;

            // Parse basic claims
            if (long.TryParse(tenantClaim, out var tId)) tc.TenantId = tId;
            if (long.TryParse(userClaim, out var uId)) tc.UserId = uId;
            tc.Role = roleClaim;
            tc.BranchName = branchNameClaim;

            if (long.TryParse(branchClaim, out var branchId))
            {
                tc.BranchId = branchId;

                // Create a scope to resolve scoped DbContext
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<EasyMoneyDbContext>();

                    try
                    {
                        var branch = await db.Branches
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(b => b.BranchId == branchId);

                        if (branch != null)
                        {
                            tc.BranchName = branch.BranchName ?? branchNameClaim;
                            tc.PreviousDate = branch.PreviousDate ?? DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
                            tc.CurrentDate = branch.CurrentDate ?? DateOnly.FromDateTime(DateTime.Now);
                            tc.NextDate = branch.NextDate ?? DateOnly.FromDateTime(DateTime.Now.AddDays(1));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching branch: {ex.Message}");
                        // Fallback to system dates
                        tc.PreviousDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
                        tc.CurrentDate = DateOnly.FromDateTime(DateTime.Now);
                        tc.NextDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
                    }
                }
            }
            else
            {
                // Fallback if branch ID not found
                tc.PreviousDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
                tc.CurrentDate = DateOnly.FromDateTime(DateTime.Now);
                tc.NextDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
            }

            Console.WriteLine($"TenantContext: TenantId={tc.TenantId}, BranchId={tc.BranchId}, CurrentDate={tc.CurrentDate}");

            if (tc.IsSifin)
            {
                tc.SetBypass(true);
            }
        }

        await _next(ctx);
    }
}
