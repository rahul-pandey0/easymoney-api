using System.Security.Cryptography;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IKycDocumentService
{
    Task<KycDocument> UploadAsync(long memberId, KycDocType docType, string? docNumber, Stream fileStream, string fileName);
    Task<IReadOnlyList<KycDocumentDto>> ListForMemberAsync(long memberId);
    Task<KycDocument> VerifyAsync(long documentId, bool verified, string? rejectionReason);
    Task<KycDocument?> GetAsync(long documentId);
}

public class KycDocumentService : IKycDocumentService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IConfiguration _cfg;
    private readonly ILogger<KycDocumentService> _log;

    public KycDocumentService(EasyMoneyDbContext db, ITenantContext ctx, IConfiguration cfg, ILogger<KycDocumentService> log)
    {
        _db = db; _ctx = ctx; _cfg = cfg; _log = log;
    }

    public async Task<KycDocument> UploadAsync(long memberId, KycDocType docType, string? docNumber, Stream fileStream, string fileName)
    {
        var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");

        // 1. Determine storage path under configured root: <root>/<tenantId>/<memberId>/<yyyy>/<filename>
        var root = _cfg["KycDocStorage:Root"]
            ?? Path.Combine(AppContext.BaseDirectory, "kyc_storage");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dir = Path.Combine(root, m.TenantId.ToString(), memberId.ToString(), today.Year.ToString());
        Directory.CreateDirectory(dir);

        // Sanitize filename: keep extension, prefix with timestamp + doc_type to avoid clashes.
        var safeName = Path.GetFileName(fileName);
        var ext = Path.GetExtension(safeName);
        var storedName = $"{docType}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var fullPath = Path.Combine(dir, storedName);

        // 2. Write file to disk while computing SHA-256.
        string hexHash;
        await using (var fs = File.Create(fullPath))
        using (var sha = SHA256.Create())
        await using (var cryptoStream = new CryptoStream(fs, sha, CryptoStreamMode.Write))
        {
            await fileStream.CopyToAsync(cryptoStream);
            await cryptoStream.FlushFinalBlockAsync();
            hexHash = Convert.ToHexString(sha.Hash!);
        }

        // 3. Persist DB row.
        var doc = new KycDocument
        {
            TenantId = m.TenantId,
            MemberId = memberId,
            DocType = docType,
            DocNumber = docNumber,
            FilePath = fullPath,
            FileHash = hexHash,
            Status = KycDocStatus.UPLOADED,
            UploadedAt = DateTime.UtcNow
        };
        _db.KycDocuments.Add(doc);

        // 4. If this was the first doc and member is PENDING, auto-advance to UNDER_REVIEW.
        if (m.KycStatus == KycStatus.PENDING)
        {
            // Direct DB write of the review row (not via KycService — we already hold the context
            // and want to avoid a transition-validation roundtrip).
            _db.KycReviews.Add(new KycReview
            {
                TenantId = m.TenantId,
                MemberId = memberId,
                FromStatus = KycStatus.PENDING,
                ToStatus = KycStatus.UNDER_REVIEW,
                Remarks = $"Auto: first document uploaded ({docType})",
                ReviewedBy = _ctx.UserId ?? 0
            });
            m.KycStatus = KycStatus.UNDER_REVIEW;
        }

        await _db.SaveChangesAsync();
        _log.LogInformation("Member {Mid} uploaded {Doc} ({Bytes}b, hash {Hash})",
            memberId, docType, new FileInfo(fullPath).Length, hexHash[..16]);
        return doc;
    }

    public async Task<IReadOnlyList<KycDocumentDto>> ListForMemberAsync(long memberId)
    {
        return await _db.KycDocuments
            .Where(d => d.MemberId == memberId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new KycDocumentDto(
                d.DocumentId, d.MemberId, d.DocType.ToString(),
                d.DocNumber, d.FilePath, d.FileHash, d.Status.ToString(),
                d.RejectionReason, d.UploadedAt, d.VerifiedAt, d.VerifiedBy))
            .ToListAsync();
    }

    public async Task<KycDocument> VerifyAsync(long documentId, bool verified, string? rejectionReason)
    {
        var d = await _db.KycDocuments.FirstOrDefaultAsync(x => x.DocumentId == documentId)
            ?? throw new DomainException($"Document {documentId} not found");
        if (d.Status != KycDocStatus.UPLOADED)
            throw new DomainException($"Document is already {d.Status}");

        if (verified)
        {
            d.Status = KycDocStatus.VERIFIED;
            d.RejectionReason = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new DomainException("Rejection reason is required");
            d.Status = KycDocStatus.REJECTED;
            d.RejectionReason = rejectionReason;
        }
        d.VerifiedBy = _ctx.UserId;
        d.VerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return d;
    }

    public Task<KycDocument?> GetAsync(long documentId) =>
        _db.KycDocuments.FirstOrDefaultAsync(x => x.DocumentId == documentId);
}
