using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/audit-log")]
[Authorize]
public class AuditLogController : ControllerBase
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;

    public AuditLogController(EasyMoneyDbContext db, ITenantContext ctx)
    {
        _db = db; _ctx = ctx;
    }

    /// <summary>
    /// GET /api/v1/audit-log
    ///   Roles: ORG_ADMIN, ORG_AUTHORIZER, AUDITOR (own tenant only)
    ///          SIFIN_* (any tenant via ?tenantId=)
    ///
    /// Query params (all optional):
    ///   entityType  — filter by entity (member, account, bid, loan, …)
    ///   entityId    — filter by specific entity row id
    ///   userId      — filter by acting user id
    ///   action      — partial match on action string
    ///   from        — UTC date-time lower bound (inclusive)
    ///   to          — UTC date-time upper bound (inclusive)
    ///   tenantId    — SIFIN users only: cross-tenant look-up
    ///   skip        — pagination offset (default 0)
    ///   take        — page size (default 50, max 200)
    /// </summary>
    [HttpGet,
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + ","
                     + Roles.SifinAdmin + "," + Roles.SifinOperator + "," + Roles.SifinAuthorizer)]
    public async Task<ActionResult<AuditLogPageDto>> List(
        [FromQuery] string? entityType,
        [FromQuery] long? entityId,
        [FromQuery] long? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] long? tenantId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var effectiveTenantId = _ctx.IsSifin ? tenantId : _ctx.TenantId;

        var q = _db.AuditLogs.AsNoTracking()
            .IgnoreQueryFilters()
            .AsQueryable();

        if (effectiveTenantId.HasValue)
            q = q.Where(a => a.TenantId == effectiveTenantId.Value);

        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(a => a.EntityType == entityType);

        if (entityId.HasValue)
            q = q.Where(a => a.EntityId == entityId.Value);

        if (userId.HasValue)
            q = q.Where(a => a.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(a => a.Action.Contains(action));

        if (from.HasValue)
            q = q.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            q = q.Where(a => a.CreatedAt <= to.Value);

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(a => new AuditLogDto(
                a.AuditId, a.TenantId, a.UserId,
                a.Action, a.EntityType, a.EntityId,
                a.OldValue, a.NewValue, a.CreatedAt))
            .ToListAsync();

        return Ok(new AuditLogPageDto(total, skip, take, items));
    }

    /// <summary>
    /// GET /api/v1/audit-log/{auditId}
    /// Returns a single audit log entry by id.
    /// </summary>
    [HttpGet("{auditId:long}"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + ","
                     + Roles.SifinAdmin + "," + Roles.SifinOperator + "," + Roles.SifinAuthorizer)]
    public async Task<ActionResult<AuditLogDto>> Get(long auditId)
    {
        var log = await _db.AuditLogs.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.AuditId == auditId)
            .Select(a => new AuditLogDto(
                a.AuditId, a.TenantId, a.UserId,
                a.Action, a.EntityType, a.EntityId,
                a.OldValue, a.NewValue, a.CreatedAt))
            .FirstOrDefaultAsync();

        if (log is null) return NotFound();

        // Non-sifin users: enforce tenant boundary manually (audit_log has no EF query filter)
        if (!_ctx.IsSifin && log.TenantId != _ctx.TenantId)
            return Forbid();

        return Ok(log);
    }
}
