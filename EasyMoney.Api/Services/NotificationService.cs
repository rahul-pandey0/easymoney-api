using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface INotificationService
{
    Task CreateAsync(long tenantId, long? userId, long? accountId, string type, string title, string body);
    Task<NotificationPageDto> ListAsync(long userId, long tenantId, bool? unreadOnly, int skip, int take);
    Task<int> MarkReadAsync(long userId, long tenantId, long[]? ids);  // null ids = mark all
    Task<int> UnreadCountAsync(long userId, long tenantId);
}

public class NotificationService : INotificationService
{
    private readonly EasyMoneyDbContext _db;

    public NotificationService(EasyMoneyDbContext db) => _db = db;

    public async Task CreateAsync(long tenantId, long? userId, long? accountId,
        string type, string title, string body)
    {
        _db.Notifications.Add(new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            AccountId = accountId,
            Type = type,
            Title = title,
            Body = body,
        });
        await _db.SaveChangesAsync();
    }

    public async Task<NotificationPageDto> ListAsync(long userId, long tenantId,
        bool? unreadOnly, int skip, int take)
    {
        // A user sees: notifications addressed to them specifically (userId == user)
        // OR broadcast notifications (userId == null) for their tenant.
        var q = _db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId
                        && (n.UserId == userId || n.UserId == null));

        if (unreadOnly == true)
            q = q.Where(n => !n.IsRead);

        var total = await q.CountAsync();
        var unread = await _db.Notifications
            .IgnoreQueryFilters()
            .CountAsync(n => n.TenantId == tenantId
                             && (n.UserId == userId || n.UserId == null)
                             && !n.IsRead);

        var items = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(n => new NotificationDto(
                n.NotificationId, n.TenantId, n.UserId, n.AccountId,
                n.Type, n.Title, n.Body, n.IsRead, n.ReadAt, n.CreatedAt))
            .ToListAsync();

        return new NotificationPageDto(total, unread, skip, take, items);
    }

    public async Task<int> MarkReadAsync(long userId, long tenantId, long[]? ids)
    {
        var q = _db.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId
                        && (n.UserId == userId || n.UserId == null)
                        && !n.IsRead);

        if (ids is { Length: > 0 })
            q = q.Where(n => ids.Contains(n.NotificationId));

        var rows = await q.ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var n in rows) { n.IsRead = true; n.ReadAt = now; }
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    public async Task<int> UnreadCountAsync(long userId, long tenantId) =>
        await _db.Notifications
            .IgnoreQueryFilters()
            .CountAsync(n => n.TenantId == tenantId
                             && (n.UserId == userId || n.UserId == null)
                             && !n.IsRead);
}
