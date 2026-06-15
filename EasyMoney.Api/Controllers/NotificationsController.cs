using EasyMoney.Api.Auth;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _svc;
    private readonly ITenantContext _ctx;

    public NotificationsController(INotificationService svc, ITenantContext ctx)
    {
        _svc = svc; _ctx = ctx;
    }

    // GET /api/v1/notifications?unreadOnly=true&skip=0&take=20
    [HttpGet]
    public async Task<ActionResult<NotificationPageDto>> List(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        if (_ctx.UserId is null || _ctx.TenantId is null)
            return Unauthorized();

        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        return Ok(await _svc.ListAsync(_ctx.UserId.Value, _ctx.TenantId.Value, unreadOnly, skip, take));
    }

    // GET /api/v1/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount()
    {
        if (_ctx.UserId is null || _ctx.TenantId is null)
            return Unauthorized();

        var count = await _svc.UnreadCountAsync(_ctx.UserId.Value, _ctx.TenantId.Value);
        return Ok(new { unread = count });
    }

    // POST /api/v1/notifications/mark-read
    // Body (optional): { "ids": [1, 2, 3] }   — omit or send empty to mark ALL read
    [HttpPost("mark-read")]
    public async Task<ActionResult<object>> MarkRead([FromBody] MarkReadRequest? req)
    {
        if (_ctx.UserId is null || _ctx.TenantId is null)
            return Unauthorized();

        var marked = await _svc.MarkReadAsync(_ctx.UserId.Value, _ctx.TenantId.Value, req?.Ids);
        return Ok(new { marked });
    }
}

public record MarkReadRequest(long[]? Ids);
