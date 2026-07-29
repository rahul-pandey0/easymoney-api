using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenants;
    private readonly ITenantContext _ctx;

    public TenantsController(ITenantService tenants, ITenantContext ctx)
    {
        _tenants = tenants; _ctx = ctx;
    }

    static TenantDto ToDto(Tenant t) => new(
        t.TenantId, t.Name,
        t.RegistrationNumber, t.Address, t.Phone,
        t.OrgEmail, t.ContactPersonName, t.ContactPersonPhone, t.StartDate, t.EffectiveDate,
        t.Status.ToString(), t.CreatedAt,t.CreatedBy, t.AuthorizedBy, t.AuthorizedAt
        ,t.SmsNotification, t.AuthorisationRequired, t.EmailNotification);

    // POST /api/v1/tenants  — SIFIN_ADMIN / SIFIN_OPERATOR creates a new tenant
    [HttpPost, Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator )]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest req)
    {
        try
        {
            var isSuperAdmin = User.IsInRole(Roles.SifinAdmin);
            var t = await _tenants.CreateTenantAsync(req, _ctx.UserId,isSuperAdmin ? _ctx.UserId : null,isSuperAdmin);
            return CreatedAtAction(nameof(Get), new { tenantId = t.TenantId }, ToDto(t));
        }
        catch (DomainException ex) { 
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/v1/tenants
    [HttpGet, Authorize(Roles = Roles.AnySifin)]
    public async Task<IActionResult> List() =>
        Ok((await _tenants.ListAsync()).Select(ToDto));



    // GET /api/v1/tenants/{tenantId}
    [HttpGet("{tenantId:long}"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.AnyOrg + "," + Roles.Auditor)]
    public async Task<IActionResult> Get(long tenantId)
    {
        if (!_ctx.IsSifin && _ctx.TenantId != tenantId) return Forbid();
        var t = await _tenants.GetAsync(tenantId);
        return t is null ? NotFound() : Ok(ToDto(t));
    }

    // GET /api/v1/tenants/{tenantId}/scheme-config
    [HttpGet("{tenantId:long}/scheme-config"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.AnyOrg + "," + Roles.Auditor)]
    public async Task<IActionResult> GetSchemeConfig(long tenantId)
    {
        if (!_ctx.IsSifin && _ctx.TenantId != tenantId) return Forbid();
        try { return Ok(await _tenants.GetSchemeConfigAsync(tenantId)); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    // PATCH /api/v1/tenants/{tenantId}/scheme-config  — update bidding/scheme parameters
    [HttpPatch("{tenantId:long}/scheme-config"),
     Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator + "," + Roles.OrgAdmin)]
    public async Task<IActionResult> UpdateSchemeConfig(long tenantId, [FromBody] SchemeConfigUpdatePayload req)
    {
        if (!_ctx.IsSifin && _ctx.TenantId != tenantId) return Forbid();
        try
        {
            await _tenants.UpdateSchemeConfigAsync(tenantId, req, _ctx.UserId);
            return Ok(await _tenants.GetSchemeConfigAsync(tenantId));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // PATCH /api/v1/tenants/{tenantId}/status  — activate / suspend / close
    [HttpPatch("{tenantId:long}/status"),
     Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator)]
    public async Task<IActionResult> SetStatus(long tenantId, [FromBody] SetTenantStatusRequest req)
    {
        if (!Enum.TryParse<TenantStatus>(req.Status, true, out var status))
            return BadRequest(new { error = $"Invalid status '{req.Status}'. Valid: ACTIVE, SUSPENDED, CLOSED" });
        try
        {
            await _tenants.SetTenantStatusAsync(tenantId, status, _ctx.UserId);
            var t = await _tenants.GetAsync(tenantId);
            return Ok(ToDto(t!));
        }        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
    // POST /api/v1/tenants/{tenantId}/scheme-config — create scheme configuration
    [HttpPost("{tenantId:long}/scheme-config")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator + "," + Roles.OrgAdmin)]
    public async Task<IActionResult> CreateScheme(long tenantId, [FromBody] SchemeConfigUpdatePayload req)
    {
        try
        {
            await _tenants.CreateSchemeConfigAsync(tenantId, req, _ctx.UserId);

            var scheme = await _tenants.GetSchemeConfigAsync(tenantId);

            return Ok(scheme);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    [HttpGet("scheme-configs")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator)]
    public async Task<ActionResult<IReadOnlyList<SchemeSummaryDto>>> GetAllSchemeConfigs()
    {
        var result = await _tenants.GetAllSchemeSummariesAsync();
        return Ok(result);
    }

    [HttpGet("scheme_tenant_product")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator)]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryDto>>> GetAllSchemedata()
    {
        var result = await _tenants.GetAllProductSummariesAsync();
        return Ok(result);
    }
    [HttpGet("scheme_tenant_product/{schemeId}")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator)]

    public async Task<ActionResult<ProductSummaryDto>> GetSchemeById(int schemeId)
    {
        try
        {
            var result = await _tenants.GetProductSummaryByIdAsync(schemeId);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
    [HttpPost("tenant_product")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator + ",")]
    public async Task<IActionResult> CreateProduct([FromBody] SchemeConfigUpdatePayload req)
    { 
        try 
        {
            await _tenants.CreateSchemeConfigAsync( req, _ctx.UserId);

            var scheme = await _tenants.GetSchemeConfigAsync();

            return Ok(scheme);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }  
    }
    [HttpPut("scheme_tenant_product/{schemeId:long}"),
   Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator + "," )]
    public async Task<IActionResult> UpdateteantProdutConfig(long schemeId, [FromBody] SchemeConfigUpdatePayload req)
    {
        try 
        {
            await _tenants.UpdateProductAsync(schemeId, req, _ctx.UserId);
            return Ok(await _tenants.GetProductConfigAsync(schemeId));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
    

    [HttpGet("general_ledger")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator)]
    public async Task<ActionResult<IReadOnlyList<GlAccountDto>>> GetAllGlata() 
    {
        var result = await _tenants.GetAllGLSummariesAsync();
        return Ok(result);
    }


    [HttpGet("{tenantId:long}/general_ledger")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator)]
    public async Task<ActionResult<GlAccountDto>> GetTenantGLAccount(long tenantId)
    {
        var result = await _tenants.GetTenantGLAccountsAsync(tenantId);

        if (result == null)
            return NotFound($"General ledger with TenantId {tenantId} not found");

        return Ok(result);
    }



    [HttpGet("general_ledger/{glId:int}"),
    Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator + ",")]
    public async Task<ActionResult<GeneralLedgerDto>> GetById(int glId)
    {
        try
        {
            var result = await _tenants.GetGLSummaryByIdAsync(glId);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("general_ledger")]
    [Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator + ",")]
    public async Task<IActionResult> CreateProduct([FromBody] GlAccountDto req)
    {
        try
        {
            await _tenants.CreateGl(req, _ctx.UserId);
             
            var gl = await _tenants.GetGlcreateAsync();

            return Ok(gl);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("general_ledger/{glId:int}"),
     Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinOperator + ",")]
    public async Task<IActionResult> UpdateGlProdutConfig(int glId, [FromBody] GlAccountDto req)
    {
        try
        {
            await _tenants.UpdateProductAsync(glId, req, _ctx.UserId);
            return Ok(await _tenants.GetGldata(glId));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }


}
