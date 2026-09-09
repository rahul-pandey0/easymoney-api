using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Ocsp;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/cycles")]
//[Authorize]
public class CyclesController : ControllerBase
{
    private readonly IBiddingService _bidding;
    private readonly ITenantContext _ctx;
    private readonly IReportService _rep;
    public CyclesController(IBiddingService bidding, ITenantContext ctx, IReportService rep)
    {
        _bidding = bidding; _ctx = ctx;  _rep = rep;
    }

    // POST /api/v1/cycles?month=2026-06-01&biddingDate=2026-06-20
    [HttpPost, Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<CycleDto>> Open(
        [FromQuery] DateOnly? month,
        [FromQuery] DateOnly? biddingDate,
        [FromQuery] long? tenantId)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required" });
        var m = month ?? DateOnly.FromDateTime(DateTime.UtcNow);
        try
        {
            var c = await _bidding.OpenCycleAsync(tid.Value, m, biddingDate);
            return Ok(ToDto(c));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("current")]
    public async Task<ActionResult<CycleDto>> Current([FromQuery] long? tenantId)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid is null) return BadRequest(new { error = "tenant_id required" });
        var c = await _bidding.GetCurrentCycleAsync(tid.Value);
        return c is null ? NotFound() : Ok(ToDto(c));
    }

    [HttpGet("{cycleId:long}")]
    public async Task<ActionResult<CycleDto>> Get(long cycleId)
    {
        var c = await _bidding.GetCycleAsync(cycleId);
        return c is null ? NotFound() : Ok(ToDto(c));
    }
    [HttpGet("{accountId:long}/accounts")]
    public async Task<ActionResult<CycleDto>> GetAccountdata(long accountId)  
    {
        var c = await _bidding.GetbidAsync(accountId);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet,
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<BidDto>> GetByData()
    {
        var bids = await _bidding.GetByData();
        return Ok(bids.Select(b => new BidDto(
            b.BidId, b.CycleId, b.AccountId, b.BidPct, b.SubmittedAt, b.UpdatedAt, b.IsWinner, b.IsApproved)));
    }
    // POST /api/v1/cycles/{id}/bids?accountId=3  — org users record bids on behalf of members
    [HttpPost("{cycleId:long}/bids"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<ActionResult<BidDto>> SubmitBid(long cycleId, [FromQuery] long accountId, [FromBody] SubmitBidReq req)
    {
        try
        {
            var b = await _bidding.SubmitOrUpdateBidAsync(accountId, req);
            return Ok(new BidDto(b.BidId, b.CycleId, b.AccountId, b.BidPct, b.SubmittedAt, b.UpdatedAt, b.IsWinner, b.IsApproved));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/v1/cycles/{id}/bids — view all bids (ranked highest first)
    [HttpGet("{cycleId:long}/bids"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> ListBids(long cycleId)
    {
        var bids = await _bidding.GetBidsAsync(cycleId);
        return Ok(bids.Select(b => new BidDto(
            b.BidId, b.CycleId, b.AccountId, b.BidPct, b.SubmittedAt, b.UpdatedAt, b.IsWinner, b.IsApproved)));
    }

    // POST /api/v1/cycles/{id}/close — operator manually closes bidding; no more bids accepted after this
    [HttpPost("{cycleId:long}/close"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer)]
    public async Task<ActionResult<CycleDto>> Close(long cycleId)
    {
        try { return Ok(ToDto(await _bidding.CloseBiddingAsync(cycleId))); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/v1/cycles/{id}/award-preview — shows pool money and all bids ranked, before awarding
    [HttpGet("{cycleId:long}/award-preview"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<AwardPreviewDto>> AwardPreview(long cycleId)
    {
        try { return Ok(await _bidding.GetAwardPreviewAsync(cycleId)); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // POST /api/v1/cycles/{id}/resolve — award to rank-1 bidder and distribute dividends
    [HttpPost("{cycleId:long}/resolve"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<CycleResolutionResultDto>> Resolve(long cycleId)
    {
        try { return Ok(await _bidding.ResolveCycleAsync(cycleId)); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private long? ResolveTenantId(long? queryTenantId) =>
        _ctx.IsSifin ? queryTenantId : _ctx.TenantId;

    private static CycleDto ToDto(BiddingCycle c) => new(
        c.CycleId, c.TenantId, c.CycleMonth, c.WindowOpenAt, c.WindowCloseAt, c.Status.ToString(),
        c.GrossCorpus, c.OrgFeeAmount, c.BidPool,
        c.WinnerAccountId, c.WinnerBidPct,
        c.LoanDisbursed, c.DividendPool, c.ResolvedAt);

    // EasyMoney.Api.Controllers/CyclesController.cs - Update these methods

    // UPDATE SubmitBid - Return BidDto with approval info
    [HttpPost("{cycleId:long}/bid"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<ActionResult<BidDto>> SubmitBids(long cycleId, [FromQuery] long accountId, [FromBody] SubmitBidReq req)
    {
        try
        {
            var b = await _bidding.SubmitOrUpdateBidAsync(accountId, req);
            return Ok(new BidDto(
                b.BidId,
                b.CycleId,
                b.AccountId,
                b.BidPct,
                b.SubmittedAt,
                b.UpdatedAt,
                b.IsWinner,
                b.IsApproved,  // ADD THIS
                b.ApprovedAt,  // ADD THIS
                b.ApprovedBy   // ADD THIS
            ));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // UPDATE ListBids - Remove the duplicate and fix the route
    [HttpGet("{cycleId:long}/bid"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> ListBids(
        long cycleId,
        [FromQuery] string? approvalStatus = null)  // ADD optional parameter
    {
        var bids = await _bidding.GetBidsAsync(cycleId, approvalStatus);
        return Ok(bids.Select(b => new BidDto(
            b.BidId,
            b.CycleId,
            b.AccountId,
            b.BidPct,
            b.SubmittedAt,
            b.UpdatedAt,
            b.IsWinner,
            b.IsApproved,  // ADD THIS
            b.ApprovedAt,  // ADD THIS
            b.ApprovedBy   // ADD THIS
        )));
    }

    // UPDATE ApproveBid - Return BidDto with approval info
    [HttpPost("{cycleId:long}/bids/{bidId:long}/approve"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<BidDto>> ApproveBid(long cycleId, long bidId)
    {
        {
            try
            {
                var bids = await _bidding.ApproveBidAsync(cycleId, bidId);

                return Ok(new
                {
                    cycleId,
                    //approvedCount = bids.Count,
                    message = "All pending bids approved successfully."
                });
            }
            catch (DomainException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
    [HttpGet("summary")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]

    public async Task<ActionResult<BiddingSummaryDto>> GetSummary()
    {
        try
        {
            var summary = await _bidding.GetCompleteBiddingSummaryAsync();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }




    [HttpGet("bonusdata")]
    //[Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer + ",")]

    public async Task<ActionResult<Bonus>> GetbonusSummary()
    {
        try
        {
            var summary = await _bidding.GetBonusDetails();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    [HttpGet("{bidrefno:long}/bonusdata")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer + ",")]
    public async Task<ActionResult<Bonus>> GetBybonusdata(string bidrefno)
    {
        try
        {
            var summary = await _bidding.GetByBonusDetails(bidrefno);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


    [HttpPost("distribute")]
    public async Task<IActionResult> DistributeBonus([FromBody] BonusDistributionRequest request)
    {
        try
        {
            var result = await _rep.DistributeBonusAsync(request);

            if (result == null || result.Status == "ERROR")
            {
                return BadRequest(new { error = result?.Message ?? "Failed to distribute bonus" });
            }

            return Ok(new
            {
                success = true,
                data = result,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("bonus-summary")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer + ",")]
    public async Task<ActionResult<BonusDistribution>> GetByBonus()
    {  
        try
        {
            var summary = await _bidding.GetBonusData();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


    // REMOVE the duplicate ListBids method at the bottom (the one with route [HttpGet("{cycleId:long}/bid")])
    // Keep only the one above with route [HttpGet("{cycleId:long}/bids")]

    //[HttpGet("{cycleId:long}/bid"),
    // Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + "," +Roles.SifinAdmin)]
    //public async Task<ActionResult<IReadOnlyList<BidDto>>> ListBids(
    //long cycleId,
    //[FromQuery] string? approvalStatus)
    //{
    //    var bids = await _bidding.GetBidsAsync(cycleId,approvalStatus);

    //    return Ok(bids.Select(b => new BidDto(
    //        b.BidId,
    //        b.CycleId,
    //        b.AccountId,
    //        b.BidPct,
    //        b.SubmittedAt,
    //        b.UpdatedAt,
    //        b.IsWinner
    //    )));
    //}


    //[HttpPost("{cycleId:long}/bids/{bidId:long}/approve"),
    //Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    //public async Task<ActionResult<BidDto>> ApproveBid(
    //long cycleId,
    //long bidId)
    //{
    //    try
    //    {
    //        var bid = await _bidding.ApproveBidAsync(
    //            cycleId,
    //            bidId);

    //        return Ok(new BidDto(
    //            bid.BidId,
    //            bid.CycleId,
    //            bid.AccountId,
    //            bid.BidPct,
    //            bid.SubmittedAt,
    //            bid.UpdatedAt,
    //            bid.IsWinner
    //        ));
    //    }
    //    catch (DomainException ex)
    //    {
    //        return BadRequest(new { error = ex.Message });
    //    }
    //}

}
