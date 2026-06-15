using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Jobs;

/// <summary>
/// Runs daily. Finds every unpaid CONTRIBUTION_DUE whose entry_date is tomorrow
/// and creates an in-app PAYMENT_DUE_REMINDER notification for the member's user account.
/// </summary>
public class DueReminderJob
{
    private readonly EasyMoneyDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<DueReminderJob> _log;

    public DueReminderJob(EasyMoneyDbContext db, INotificationService notifications,
        ILogger<DueReminderJob> log)
    {
        _db = db; _notifications = notifications; _log = log;
    }

    public async Task ExecuteAsync()
    {
        // Target: dues falling due tomorrow (so members see the reminder today)
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        _log.LogInformation("DueReminderJob running for due date {Date}", tomorrow);

        // Find all unpaid CONTRIBUTION_DUE entries for tomorrow
        var dues = await _db.LedgerEntries
            .IgnoreQueryFilters()
            .Where(l => l.EntryType == LedgerEntryType.CONTRIBUTION_DUE
                        && l.EntryDate == tomorrow)
            .Select(l => new { l.EntryId, l.TenantId, l.AccountId, l.Amount })
            .ToListAsync();

        if (dues.Count == 0)
        {
            _log.LogInformation("DueReminderJob: no dues found for {Date}", tomorrow);
            return;
        }

        // Exclude already-paid dues (linked payment exists)
        var dueIds = dues.Select(d => d.EntryId).ToList();
        var paidDueIds = await _db.LedgerEntries
            .IgnoreQueryFilters()
            .Where(l => l.EntryType == LedgerEntryType.PAYMENT_RECEIVED
                        && l.LinkedEntryId != null
                        && dueIds.Contains(l.LinkedEntryId.Value))
            .Select(l => l.LinkedEntryId!.Value)
            .Distinct()
            .ToListAsync();

        var unpaid = dues.Where(d => !paidDueIds.Contains(d.EntryId)).ToList();
        _log.LogInformation("DueReminderJob: {Total} dues tomorrow, {Unpaid} unpaid", dues.Count, unpaid.Count);

        // Look up the member user for each account
        var accountIds = unpaid.Select(d => d.AccountId).Distinct().ToList();
        var memberIdByAccount = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => accountIds.Contains(a.AccountId))
            .Select(a => new { a.AccountId, a.AccountNumber, a.MemberId })
            .ToDictionaryAsync(a => a.AccountId);

        var memberIds = memberIdByAccount.Values.Select(a => a.MemberId).Distinct().ToList();
        var userIdByMember = await _db.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.MemberId != null && memberIds.Contains(u.MemberId.Value) && u.IsActive)
            .Select(u => new { u.MemberId, u.UserId })
            .ToDictionaryAsync(u => u.MemberId!.Value, u => u.UserId);

        int created = 0;
        foreach (var due in unpaid)
        {
            if (!memberIdByAccount.TryGetValue(due.AccountId, out var acct)) continue;
            userIdByMember.TryGetValue(acct.MemberId, out var userId);

            // userId may be null if the member has no login yet — broadcast to org instead
            // by leaving userId null (org admin/operator will see it)
            try
            {
                await _notifications.CreateAsync(
                    tenantId: due.TenantId,
                    userId: userId == 0 ? null : userId,
                    accountId: due.AccountId,
                    type: NotificationTypes.PaymentDueReminder,
                    title: "Payment Due Tomorrow",
                    body: $"Account {acct.AccountNumber}: ₹{due.Amount:N0} installment due on {tomorrow:dd MMM yyyy}.");
                created++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "DueReminderJob failed for accountId={AccountId}", due.AccountId);
            }
        }

        _log.LogInformation("DueReminderJob: created {Count} reminders for {Date}", created, tomorrow);
    }
}
