using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/members/{memberId:long}")]
[Authorize]
public class KycController : ControllerBase
{
    private readonly IKycService _kyc;
    private readonly IKycDocumentService _docs; 
    private readonly ITenantContext _ctx;
    private readonly ILogger<KycController> _log;


    public KycController(IKycService kyc, IKycDocumentService docs, ITenantContext ctx, ILogger<KycController> log)
    {
        _kyc = kyc; _docs = docs; ctx = _ctx; _log = log;
    }

    // ============================================================
    // Individual / Corporate detail
    // =======================================================s=====
    [HttpPut("kyc/individual-detail"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<IActionResult> UpsertIndividual(long memberId, [FromBody] UpsertIndividualKycRequest req)
    {
        try
        {
            await _kyc.UpsertIndividualDetailAsync(memberId, req);
            return Ok(await _kyc.GetIndividualDetailAsync(memberId));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("kyc/individual-detail")]
    public async Task<IActionResult> GetIndividual(long memberId)
    {
        var d = await _kyc.GetIndividualDetailAsync(memberId);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpPut("kyc/corporate-detail"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<IActionResult> UpsertCorporate(long memberId, [FromBody] UpsertCorporateKycRequest req)
    {
        try
        {
            await _kyc.UpsertCorporateDetailAsync(memberId, req);
            return Ok(await _kyc.GetCorporateDetailAsync(memberId));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("kyc/corporate-detail")]
    public async Task<IActionResult> GetCorporate(long memberId)
    {
        var d = await _kyc.GetCorporateDetailAsync(memberId);
        return d is null ? NotFound() : Ok(d);
    }

    // ============================================================
    // Tier promotion
    // ============================================================
    [HttpPost("kyc/promote-to-full"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer)]
    public async Task<IActionResult> PromoteToFull(long memberId)
    {
        try
        {
            await _kyc.PromoteToFullAsync(memberId);
            return Ok(new { memberId, kycTier = "FULL" });
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ============================================================
    // KYC documents
    // ============================================================
    [HttpPost("kyc-documents"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.Member)]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20MB
    public async Task<IActionResult> UploadDocument(
        long memberId,
        [FromForm] KycDocType docType,
        [FromForm] string? docNumber,
        IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "file is required" });
        try
        {
            await using var s = file.OpenReadStream();
            var doc = await _docs.UploadAsync(memberId, docType, docNumber, s, file.FileName);
            return Ok(new KycDocumentDto(
                doc.DocumentId, doc.MemberId, doc.DocType.ToString(),
                doc.DocNumber, doc.FilePath, doc.FileHash, doc.Status.ToString(),
                doc.RejectionReason, doc.UploadedAt, doc.VerifiedAt, doc.VerifiedBy));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("kyc-documents")]
    public async Task<ActionResult<IReadOnlyList<KycDocumentDto>>> ListDocuments(long memberId)
        => Ok(await _docs.ListForMemberAsync(memberId));

    [HttpPut("kyc-documents/{documentId:long}/verify"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer)]
    public async Task<IActionResult> VerifyDocument(long memberId, long documentId, [FromBody] VerifyKycDocumentRequest req)
    {
        try
        {
            var d = await _docs.VerifyAsync(documentId, req.Verified, req.RejectionReason);
            if (d.MemberId != memberId) return NotFound();
            return Ok(new KycDocumentDto(
                d.DocumentId, d.MemberId, d.DocType.ToString(),
                d.DocNumber, d.FilePath, d.FileHash, d.Status.ToString(),
                d.RejectionReason, d.UploadedAt, d.VerifiedAt, d.VerifiedBy));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ============================================================
    // KYC review (state transitions)
    // ============================================================
    [HttpPost("kyc-review"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer)]
    public async Task<IActionResult> Review(long memberId, [FromBody] KycReviewRequest req)
    {
        try
        {
            var r = await _kyc.ReviewAsync(memberId, req.ToStatus, req.Remarks);
            return Ok(new KycReviewDto(
                r.ReviewId, r.MemberId,
                r.FromStatus.ToString(), r.ToStatus.ToString(),
                r.Remarks, r.ReviewedBy, r.ReviewedAt));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("kyc-review")]
    public async Task<ActionResult<IReadOnlyList<KycReviewDto>>> History(long memberId)
        => Ok(await _kyc.GetReviewHistoryAsync(memberId));

    [HttpGet("{documentId:long}/preview"),
        Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.OrgAuthorizer)]
    public async Task<IActionResult> Preview(long memberId, long documentId)
    {
        var doc = await _kyc.GetAsync(documentId);
        if (doc == null || doc.MemberId != memberId) return NotFound();

        if (string.IsNullOrWhiteSpace(doc.FilePath))
            return NotFound("No file path stored");

        // Normalize separators ("\\" and "/")
        var normalized = doc.FilePath.Replace('\\', '/');

        if (!System.IO.File.Exists(normalized))
        {
            _log.LogWarning("KYC file not found: {Path}", normalized);
            return NotFound($"File not found on disk: {normalized}");
        }

        var ext = Path.GetExtension(normalized).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };

        var bytes = await System.IO.File.ReadAllBytesAsync(normalized);
        var base64 = Convert.ToBase64String(bytes);

        return Ok(new
        {
            fileName = Path.GetFileName(normalized),
            contentType,
            base64,
            // convenience: full data URL you can drop straight into <img src>
            dataUrl = $"data:{contentType};base64,{base64}"
        });
    }
}
