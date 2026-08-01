using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/approval-requests")]
[Authorize]
public class ApprovalRequestsController : ControllerBase
{
    private readonly IApprovalService _approvals;
    public ApprovalRequestsController(IApprovalService approvals) => _approvals = approvals;

    [HttpPost]
    public async Task<ActionResult<ApprovalRequestDto>> Submit([FromBody] CreateApprovalRequest req, [FromQuery] long? tenantId)
    {
        try
        {
            var r = await _approvals.SubmitAsync(req.ActionType, req.EntityType, req.EntityId, req.Payload, tenantId);
            return Ok(ToDto(r));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{requestId:long}")]
    public async Task<ActionResult<ApprovalRequestDto>> Get(long requestId)
    {
        var r = await _approvals.GetAsync(requestId);
        return r is null ? NotFound() : Ok(ToDto(r));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalRequestDto>>> List(
        [FromQuery] ApprovalStatus? status,
        [FromQuery] ApprovalActionType? actionType,
        [FromQuery] long? tenantId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
        => Ok(await _approvals.ListAsync(status, actionType, tenantId, skip, take));

    [HttpPost("{requestId:long}/decision"),
     Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinAuthorizer + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer)]
    public async Task<ActionResult<ApprovalRequestDto>> Decide(long requestId, [FromBody] ApprovalDecisionRequest req)
    {
        try
        {
            var r = await _approvals.DecideAsync(requestId, req.Approve, req.Remarks);
            return Ok(ToDto(r));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private static ApprovalRequestDto ToDto(ApprovalRequest r) => new(
        r.RequestId, r.TenantId, r.ActionType.ToString(),
        r.EntityType, r.EntityId, r.Payload, r.Status.ToString(),
        r.RequestedBy, r.RequestedAt,
        r.DecidedBy, r.DecidedAt, r.DecisionRemarks);
}
