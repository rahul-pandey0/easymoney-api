using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _acc;
    private readonly ITenantContext _ctx;

    public AccountingController(IAccountingService acc, ITenantContext ctx)
    {
        _acc = acc; _ctx = ctx;
    }

    // ============================================================
    // Chart of accounts (per tenant)
    // ============================================================
    [HttpGet("reports/gl/chart"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor)]
    public async Task<ActionResult<IReadOnlyList<GlAccountDto>>> GetChart([FromQuery] long? tenantId)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required (query or JWT claim)" });
        return Ok(await _acc.GetChartOfAccountsAsync(tid.Value));
    }

    // ============================================================
    // GL ledger for a specific GL account code
    // ============================================================
    [HttpGet("reports/gl/{code}/ledger"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor)]
    public async Task<ActionResult<GlLedgerDto>> GetGlLedger(
        string code, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] long? tenantId)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required" });
        try { return Ok(await _acc.GetGlLedgerAsync(tid.Value, code, fromDate: from, toDate: to)); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ============================================================
    // Member-account GL ledger
    // ============================================================
    [HttpGet("accounts/{accountId:long}/gl-ledger"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.OrgOperator + "," + Roles.Auditor + "," + Roles.Member)]
    public async Task<ActionResult<MemberAccountLedgerDto>> GetMemberAccountLedger(
        long accountId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        try { return Ok(await _acc.GetMemberAccountLedgerAsync(accountId, fromDate: from, toDate: to)); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ============================================================
    // Trial balance, balance sheet, income statement
    // ============================================================
    [HttpGet("reports/trial-balance"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor)]
    public async Task<ActionResult<TrialBalanceDto>> TrialBalance([FromQuery] DateOnly? asOf, [FromQuery] long? tenantId)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required" });
        return Ok(await _acc.GetTrialBalanceAsync(tid.Value, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [HttpGet("reports/balance-sheet"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor)]
    public async Task<ActionResult<BalanceSheetDto>> BalanceSheet([FromQuery] DateOnly? asOf, [FromQuery] long? tenantId)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required" });
        return Ok(await _acc.GetBalanceSheetAsync(tid.Value, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [HttpGet("reports/income-statement"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor)]
    public async Task<ActionResult<IncomeStatementDto>> IncomeStatement(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] long? tenantId)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required" });
        return Ok(await _acc.GetIncomeStatementAsync(tid.Value, from, to));
    }

    // ============================================================
    // Manual journal posting (MANUAL_ADJUSTMENT only, audited)
    // Restricted to AUTHORIZERS at SIFIN or ORG level so the journal
    // has a real authorizer at post time.
    // ============================================================
    [HttpPost("journals"),
     Authorize(Roles = Roles.SifinAdmin + "," + Roles.SifinAuthorizer + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer)]
    public async Task<ActionResult<PostJournalResult>> PostManualJournal(
        [FromBody] PostJournalRequest req, [FromQuery] long? tenantId)
    {
        if (req.SourceType != JournalSourceType.MANUAL_ADJUSTMENT)
            return BadRequest(new { error = "API-posted journals must use sourceType=MANUAL_ADJUSTMENT" });
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required" });
        try
        {
            var jid = await _acc.PostJournalAsync(
                tid.Value, req.EntryDate, req.SourceType, req.SourceId,
                req.PaymentMethod, req.Description, req.Lines,
                createdBy: _ctx.UserId,
                authorizedBy: _ctx.UserId);   // direct-post: caller is also the authorizer
            var td = req.Lines.Sum(l => l.Debit);
            var tc = req.Lines.Sum(l => l.Credit);
            return Ok(new PostJournalResult(jid, req.Lines.Count, td, tc));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ============================================================
    // Helper: resolve effective tenant id.
    //   SIFIN role: must pass ?tenantId=
    //   Org role: forced to user's own tenant_id (claim)
    // ============================================================
    private long? ResolveTenantId(long? queryTenantId)
    {
        if (_ctx.IsSifin) return queryTenantId;
        return _ctx.TenantId;
    }
}
