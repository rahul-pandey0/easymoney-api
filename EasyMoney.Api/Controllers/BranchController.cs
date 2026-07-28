using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/branches")]
[Authorize]
public class BranchController : ControllerBase
{
    private readonly IBranchService _branches;
    private readonly ITenantContext _ctx;

    public BranchController(IBranchService branches, ITenantContext ctx)
    {
        _branches = branches;
        _ctx = ctx;
    }

    // POST /api/v1/branch  — SIFIN_ADMIN / SIFIN_OPERATOR creates a new branch
    [HttpPost, Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator)]
    public async Task<ActionResult<BranchDto>> Create([FromBody] CreateBranchRequest req)
    {
        try
        {
            var branch = await _branches.CreateAsync(req);
            return CreatedAtAction(nameof(Get), new { branchId = branch.BranchId }, branch);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{branchId:long}")]
    public async Task<ActionResult<BranchDto>> Get(long branchId)
    {
        var branch = await _branches.GetByIdAsync(branchId);

        if (branch is null)
            return NotFound();

        return Ok(branch);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> List()
    {
        return Ok(await _branches.GetAllAsync());
    }
}