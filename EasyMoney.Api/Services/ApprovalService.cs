using System.Text.Json;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

/// <summary>
/// Maker-Checker orchestration. Every controlled action goes through this service:
///   1. SubmitAsync (maker) — writes an approval_request row.
///      If the tenant has scheme_config.maker_checker_enabled = FALSE (and the action
///      is not SIFIN-level), the request is immediately applied and stamped AUTO_APPROVED.
///      Otherwise it stays PENDING until a checker calls DecideAsync.
///   2. DecideAsync (checker) — must be a different user from RequestedBy.
///      On APPROVED, the payload is applied via ApplyApprovedAsync (per action_type).
///      On REJECTED, the entity is unchanged; only the request row + audit log are written.
///
/// SIFIN-level actions (TENANT_CREATE, TENANT_STATUS_CHANGE, MANUAL_JOURNAL_ADJUSTMENT
/// when no tenant scope) ALWAYS require a SIFIN_AUTHORIZER, regardless of any flag.
/// </summary>
public interface IApprovalService
{
    Task<ApprovalRequest> SubmitAsync(ApprovalActionType actionType, string entityType, long? entityId, object payload, long? tenantId = null);
    Task<ApprovalRequest> DecideAsync(long requestId, bool approve, string? remarks);
    Task<ApprovalRequest?> GetAsync(long requestId);
    Task<IReadOnlyList<ApprovalRequestDto>> ListAsync(ApprovalStatus? status, ApprovalActionType? actionType, long? tenantId, int skip, int take);
}

public class ApprovalService : IApprovalService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IServiceProvider _sp;
    private readonly ILogger<ApprovalService> _log;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ApprovalService(EasyMoneyDbContext db, ITenantContext ctx, IServiceProvider sp, ILogger<ApprovalService> log)
    {
        _db = db; _ctx = ctx; _sp = sp; _log = log;
    }

    // ============================================================
    // Submit (maker)
    // ============================================================
    public async Task<ApprovalRequest> SubmitAsync(
        ApprovalActionType actionType, string entityType, long? entityId, object payload, long? tenantId = null)
    {
        var requestedBy = _ctx.UserId
            ?? throw new DomainException("Authenticated user required to submit approval");

        // Resolve effective tenant: SIFIN-level actions stay tenant_id=NULL,
        // otherwise default to caller's tenant claim.
        bool isSifinAction = actionType is ApprovalActionType.TENANT_CREATE
                                          or ApprovalActionType.TENANT_STATUS_CHANGE;
        long? effectiveTenant = isSifinAction ? null : (tenantId ?? _ctx.TenantId);
        if (!isSifinAction && effectiveTenant is null)
            throw new DomainException("Tenant scope required for this action");

        var payloadJson = JsonSerializer.Serialize(payload, Json);

        // Should this short-circuit through AUTO_APPROVED?
        bool autoApprove = false;
        if (!isSifinAction && effectiveTenant.HasValue)
        {
            var scheme = await _db.SchemeConfigs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == effectiveTenant.Value);
            if (scheme is null)
                throw new DomainException($"Tenant {effectiveTenant} has no scheme_config");
            autoApprove = !scheme.MakerCheckerEnabled;
        }

        var req = new ApprovalRequest
        {
            TenantId = effectiveTenant,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payloadJson,
            Status = ApprovalStatus.PENDING,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow
        };
        _db.ApprovalRequests.Add(req);
        await _db.SaveChangesAsync();

        await WriteAuditAsync(req, "APPROVAL_REQUEST_CREATED", oldValue: null, newValue: payloadJson);
        _log.LogInformation("Approval request {Rid} created (action={Action}, tenant={Tid}, by user {Uid})",
            req.RequestId, actionType, effectiveTenant, requestedBy);

        // Short-circuit if maker-checker disabled (AUTO_APPROVED, same user is decider)
        if (autoApprove)
        {
            await ApplyApprovedAsync(req);
            req.Status = ApprovalStatus.AUTO_APPROVED;
            req.DecidedBy = requestedBy;
            req.DecidedAt = DateTime.UtcNow;
            req.DecisionRemarks = "Auto-approved (tenant maker_checker_enabled=false)";
            await _db.SaveChangesAsync();
            await WriteAuditAsync(req, "APPROVAL_REQUEST_AUTO_APPROVED", oldValue: payloadJson, newValue: payloadJson);
        }

        return req;
    }

    // ============================================================
    // Decide (checker)
    // ============================================================
    public async Task<ApprovalRequest> DecideAsync(long requestId, bool approve, string? remarks)
    {
        var deciderId = _ctx.UserId
            ?? throw new DomainException("Authenticated user required to decide approval");

        var req = await _db.ApprovalRequests.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.RequestId == requestId)
            ?? throw new DomainException($"Approval request {requestId} not found");

        if (req.Status != ApprovalStatus.PENDING)
            throw new DomainException($"Request is already {req.Status}");
        if (req.RequestedBy == deciderId)
            throw new DomainException("Checker must differ from requester");

        // Role gating: SIFIN-level actions require SIFIN_AUTHORIZER or SIFIN_ADMIN.
        //              Org actions require ORG_AUTHORIZER or ORG_ADMIN (within the same tenant).
        var roleStr = _ctx.Role;
        bool isSifinAction = req.ActionType is ApprovalActionType.TENANT_CREATE
                                            or ApprovalActionType.TENANT_STATUS_CHANGE;
        if (isSifinAction)
        {
            if (roleStr is not (Roles.SifinAdmin or Roles.SifinAuthorizer))
                throw new DomainException("SIFIN-level actions require SIFIN_AUTHORIZER or SIFIN_ADMIN");
        }
        else
        {
            if (roleStr is not (Roles.OrgAdmin or Roles.OrgAuthorizer or Roles.SifinAdmin))
                throw new DomainException("Only ORG_AUTHORIZER, ORG_ADMIN, or SIFIN_ADMIN can decide org actions");
            if (roleStr is (Roles.OrgAdmin or Roles.OrgAuthorizer)
                && _ctx.TenantId != req.TenantId)
                throw new DomainException("Cannot decide approval for a different tenant");
        }

        if (approve)
        {
            await ApplyApprovedAsync(req);
            req.Status = ApprovalStatus.APPROVED;
        }
        else
        {
            req.Status = ApprovalStatus.REJECTED;
        }
        req.DecidedBy = deciderId;
        req.DecidedAt = DateTime.UtcNow;
        req.DecisionRemarks = remarks;
        await _db.SaveChangesAsync();

        await WriteAuditAsync(req,
            approve ? "APPROVAL_REQUEST_APPROVED" : "APPROVAL_REQUEST_REJECTED",
            oldValue: req.Payload,
            newValue: JsonSerializer.Serialize(new { req.Status, req.DecisionRemarks }, Json));
        _log.LogInformation("Approval request {Rid} {Status} by user {Uid}",
            req.RequestId, req.Status, deciderId);

        return req;
    }

    public Task<ApprovalRequest?> GetAsync(long requestId) =>
        _db.ApprovalRequests.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.RequestId == requestId);

    public async Task<IReadOnlyList<ApprovalRequestDto>> ListAsync(
        ApprovalStatus? status, ApprovalActionType? actionType, long? tenantId, int skip, int take)
    {
        var q = _db.ApprovalRequests.IgnoreQueryFilters().AsQueryable();
        // Role scoping for listing
        if (_ctx.Role is Roles.OrgAdmin or Roles.OrgAuthorizer or Roles.OrgOperator or Roles.Auditor)
            q = q.Where(r => r.TenantId == _ctx.TenantId);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        if (actionType.HasValue) q = q.Where(r => r.ActionType == actionType.Value);
        if (tenantId.HasValue && _ctx.IsSifin) q = q.Where(r => r.TenantId == tenantId.Value);

        return await q.OrderByDescending(r => r.RequestedAt)
            .Skip(skip).Take(Math.Clamp(take, 1, 200))
            .Select(r => new ApprovalRequestDto(
                r.RequestId, r.TenantId, r.ActionType.ToString(),
                r.EntityType, r.EntityId, r.Payload, r.Status.ToString(),
                r.RequestedBy, r.RequestedAt,
                r.DecidedBy, r.DecidedAt, r.DecisionRemarks))
            .ToListAsync();
    }

    // ============================================================
    // ApplyApprovedAsync — dispatch by action_type
    // ============================================================
    private async Task ApplyApprovedAsync(ApprovalRequest req)
    {
        switch (req.ActionType)
        {
            //case ApprovalActionType.TENANT_CREATE:
            //{
            //    var p = Parse<TenantCreatePayload>(req.Payload);
            //    var tenantSvc = _sp.GetRequiredService<ITenantService>();
            //    var createReq = new CreateTenantRequest(p.Name, p.RegistrationNumber, p.Address, p.Phone, p.OrgEmail, p.ContactPersonName, p.ContactPersonPhone,p.StartDate, p.EffectiveDate);
            //    var t = await tenantSvc.CreateTenantAsync(createReq, createdBy: req.RequestedBy, authorizedBy: _ctx.UserId, isSuperAdmin: true);
            //    req.EntityId = t.TenantId;
            //    break;
            //}
            case ApprovalActionType.TENANT_CREATE:
                {
                    if (!req.EntityId.HasValue)
                        throw new DomainException("Tenant id required");

                    var tenantSvc = _sp.GetRequiredService<ITenantService>();

                    await tenantSvc.SetTenantStatusAsync(
                        req.EntityId.Value,
                        TenantStatus.ACTIVE,
                        _ctx.UserId);

                    break;
                }
            case ApprovalActionType.TENANT_STATUS_CHANGE:
            {
                var p = Parse<TenantStatusChangePayload>(req.Payload);
                if (!req.EntityId.HasValue) throw new DomainException("entity_id required for TENANT_STATUS_CHANGE");
                if (!Enum.TryParse<TenantStatus>(p.Status, true, out var st))
                    throw new DomainException("Invalid status");
                var tenantSvc = _sp.GetRequiredService<ITenantService>();
                await tenantSvc.SetTenantStatusAsync(req.EntityId.Value, st, _ctx.UserId);
                break;
            }
            case ApprovalActionType.SCHEME_CONFIG_UPDATE:
            {
                var p = Parse<SchemeConfigUpdatePayload>(req.Payload);
                if (!req.TenantId.HasValue) throw new DomainException("tenant_id required for SCHEME_CONFIG_UPDATE");
                var tenantSvc = _sp.GetRequiredService<ITenantService>();
                await tenantSvc.UpdateSchemeConfigAsync(req.TenantId.Value, p, _ctx.UserId);
                break;
            }
            case ApprovalActionType.USER_CREATE:
            {
                var p = Parse<UserCreatePayload>(req.Payload);
                if (!Enum.TryParse<UserRole>(p.Role, true, out var role))
                    throw new DomainException("Invalid role");
                var authSvc = _sp.GetRequiredService<IAuthService>();
                var u = await authSvc.CreateUserAsync(p.TenantId ?? req.TenantId, p.Email, p.Password, role,
                    p.MemberId, p.BranchId, createdBy: req.RequestedBy, preAuthorized: true);
                req.EntityId = u.UserId;
                break;
            }
            case ApprovalActionType.MANUAL_JOURNAL_ADJUSTMENT:
            {
                var p = Parse<ManualJournalAdjustmentPayload>(req.Payload);
                if (!Enum.TryParse<PaymentMethod>(p.PaymentMethod, true, out var pm))
                    throw new DomainException("Invalid paymentMethod");
                var acc = _sp.GetRequiredService<IAccountingService>();
                var jid = await acc.PostJournalAsync(
                    p.TenantId, p.EntryDate, JournalSourceType.MANUAL_ADJUSTMENT,
                    sourceId: null, pm, p.Description, p.Lines,
                    createdBy: req.RequestedBy, authorizedBy: _ctx.UserId);
                req.EntityId = jid;
                break;
            }
            case ApprovalActionType.MEMBER_KYC_APPROVAL:
            {
                var p = Parse<MemberKycApprovalPayload>(req.Payload);
                if (!Enum.TryParse<KycStatus>(p.ToStatus, true, out var toStatus))
                    throw new DomainException($"Invalid KYC status '{p.ToStatus}'");
                var kycSvc = _sp.GetRequiredService<IKycService>();
                var review = await kycSvc.ReviewAsync(p.MemberId, toStatus, p.Remarks);
                req.EntityId = review.ReviewId;
                break;
            }
            case ApprovalActionType.ACCOUNT_OPEN:
            {
                var p = Parse<AccountOpenPayload>(req.Payload);
                var accountSvc = _sp.GetRequiredService<IAccountService>();
                    var account = await accountSvc.OpenAccountAsync(
                    p.MemberId, p.MonthlyContribution, p.AccountOpenDate,
                    createdBy: req.RequestedBy, authorizedBy: _ctx.UserId, payload: p);             
                    req.EntityId = account.AccountId;
                break;
            }
            case ApprovalActionType.LOAN_DISBURSEMENT:
            {
                var p = Parse<LoanDisbursementPayload>(req.Payload);
                var loanSvc = _sp.GetRequiredService<ILoanService>();
                var loan = await loanSvc.DisburseAsync(p.AccountId, p.CycleId, p.Principal);
                req.EntityId = loan.LoanId;
                break;
            }
            case ApprovalActionType.CYCLE_RESOLUTION_OVERRIDE:
            {
                var p = Parse<CycleResolutionOverridePayload>(req.Payload);
                var biddingSvc = _sp.GetRequiredService<IBiddingService>();
                var result = await biddingSvc.ResolveCycleAsync(p.CycleId);
                req.EntityId = result.CycleId;
                break;
            }

            case ApprovalActionType.BID_APPROVE:
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(req.Payload);

                    var cycleId = payload.GetProperty("CycleId").GetInt64();
                    var bidId = payload.GetProperty("BidId").GetInt64();
                    var biddingSvc = _sp.GetRequiredService<IBiddingService>();
                    await biddingSvc.ApproveBidAsync(cycleId, bidId);
                    //await biddingSvc.CloseBiddingAsync(cycleId);

                    req.EntityId = cycleId;
                    break;
                }
            case ApprovalActionType.EXIT_PROCESS:
            {
                var p = Parse<ExitProcessPayload>(req.Payload);
                if (!Enum.TryParse<PaymentMethod>(p.RefundMethod, true, out var refundMethod))
                    throw new DomainException($"Invalid refund method '{p.RefundMethod}'");
                var exitSvc = _sp.GetRequiredService<IExitService>();
                var result = await exitSvc.ProcessExitAsync(p.AccountId, p.ExitDate, refundMethod);
                req.EntityId = result.AccountId;
                break;
            }
            case ApprovalActionType.USER_ROLE_CHANGE:
            {
                var p = Parse<UserRoleChangePayload>(req.Payload);
                if (!Enum.TryParse<UserRole>(p.NewRole, true, out var newRole))
                    throw new DomainException($"Invalid role '{p.NewRole}'");
                if (!req.EntityId.HasValue) throw new DomainException("entity_id (userId) required for USER_ROLE_CHANGE");
                var authSvc = _sp.GetRequiredService<IAuthService>();
                await authSvc.ChangeUserRoleAsync(req.EntityId.Value, newRole, _ctx.UserId);
                break;
            }

            case ApprovalActionType.CREATE_LOAN:
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(req.Payload);

                    var cycleId = payload.GetProperty("CycleId").GetInt64();
                    var loanId = payload.GetProperty("LoanId").GetInt64();
                    var biddingSvc = _sp.GetRequiredService<ILoanService>();
                    await biddingSvc.ApproveLoanAsync(loanId);
                    req.EntityId = cycleId;
                    break;
                }

            default:
                throw new DomainException($"Unknown action type {req.ActionType}");
        }
    }

    private static T Parse<T>(string json)
    {
        var v = JsonSerializer.Deserialize<T>(json, Json)
            ?? throw new DomainException($"Invalid payload for {typeof(T).Name}");
        return v;
    }

    private async Task WriteAuditAsync(ApprovalRequest req, string action, string? oldValue, string? newValue)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = req.TenantId,
            UserId = _ctx.UserId,
            Action = action,
            EntityType = "approval_request",
            EntityId = req.RequestId,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
