using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/members")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IMemberService _members;
    private readonly ITenantContext _ctx;

    public MembersController(IMemberService members, ITenantContext ctx)
    {
        _members = members; _ctx = ctx;
    }

    [HttpPost, Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<ActionResult<MemberDto>> Create([FromBody] CreateMemberRequest req)
    {
        try
        {
            var m = await _members.CreateAsync(req);
            return CreatedAtAction(nameof(Get), new { memberId = m.MemberId }, MemberService.ToDto(m));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{memberId:long}")]
    public async Task<ActionResult<MemberDto>> Get(long memberId)
    {
        var m = await _members.GetAsync(memberId);
        if (m is null) return NotFound();
        return Ok(MemberService.ToDto(m));
    }


    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> List(
        [FromQuery] string? search, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        => Ok(await _members.ListAsync(search, skip, take));

    [HttpPut("{memberId:long}"), Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<ActionResult<MemberDto>> Update(long memberId, [FromBody] UpdateMemberRequest req)
    {
        try
        {
            var m = await _members.UpdateAsync(memberId, req);
            return Ok(MemberService.ToDto(m));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
