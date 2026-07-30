using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IMemberService
{
    Task<Member> CreateAsync(CreateMemberRequest req);
    Task<Member?> GetAsync(long memberId);     
    //Task<IReadOnlyList<MemberDto>> ListAsync(string? search, int skip, int take);
    Task<PagedResult<MemberDto>> ListAsync(string? search, int skip, int take);
    Task<Member> UpdateAsync(long memberId, UpdateMemberRequest req);
}


public class MemberService : IMemberService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IKycService _kyc;
    private readonly ILogger<MemberService> _log;

    public MemberService(EasyMoneyDbContext db, ITenantContext ctx, IKycService kyc, ILogger<MemberService> log)
    {
        _db = db; _ctx = ctx; _kyc = kyc; _log = log;
    }
    private async Task<string?> SaveFileAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"uploads/{fileName}";
    }

    public async Task<Member> CreateAsync(CreateMemberRequest req)
    {
        if (_ctx.TenantId is null)
            throw new DomainException("Tenant context required to create member");

        var m = new Member
        {
            TenantId = _ctx.TenantId.Value,
            MemberType = req.MemberType,
            FullName = req.FullName,
            Phone = req.Phone,
            Email = req.Email,
            PanNumber = req.PanNumber,
            AddressLine = req.AddressLine,
            City = req.City,
            State = req.State,
            Pincode = req.Pincode,
            BankAccountNo = req.BankAccountNo,
            BankIfsc = req.BankIfsc,
            BankHolderName = req.BankHolderName,
            KycTier = KycTier.MINIMAL,
            KycStatus = KycStatus.PENDING,
           
        };
        _db.Members.Add(m);
        await _db.SaveChangesAsync();
        _log.LogInformation("Created member {Mid} (tenant {Tid}, type {Type})",
            m.MemberId, m.TenantId, m.MemberType);
        return m;
    }

    public Task<Member?> GetAsync(long memberId) =>
        _db.Members.FirstOrDefaultAsync(m => m.MemberId == memberId);

    //public async Task<IReadOnlyList<MemberDto>> ListAsync(string? search, int skip, int take)
    //{
    //    var q = _db.Members.AsQueryable();
    //    if (!string.IsNullOrWhiteSpace(search))
    //    {
    //        q = q.Where(m => m.FullName.Contains(search)
    //                         || (m.Phone != null && m.Phone.Contains(search))
    //                         || (m.Email != null && m.Email.Contains(search)));
    //    }
    //    return await q.OrderByDescending(m => m.CreatedAt)
    //        .Skip(skip).Take(Math.Clamp(take, 1, 200))
    //        .Select(m => ToDto(m))
    //        .ToListAsync();
    //}
    public async Task<PagedResult<MemberDto>> ListAsync(string? search, int skip, int take)
    {
        var q = _db.Members.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(m =>
                m.FullName.Contains(search) ||
                (m.Phone != null && m.Phone.Contains(search)) ||
                (m.Email != null && m.Email.Contains(search)));
        }

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 200))
            .Select(m => ToDto(m))
            .ToListAsync();

        return new PagedResult<MemberDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<Member> UpdateAsync(long memberId, UpdateMemberRequest req)
    {
        var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");

        bool identityChanged = false;
        if (!string.IsNullOrWhiteSpace(req.FullName) && req.FullName != m.FullName)
        { m.FullName = req.FullName; identityChanged = true; }
        if (!string.IsNullOrWhiteSpace(req.Phone) && req.Phone != m.Phone)
        { m.Phone = req.Phone; identityChanged = true; }
        if (req.Email is not null) m.Email = req.Email;
        if (req.AddressLine is not null) m.AddressLine = req.AddressLine;
        if (req.City is not null) m.City = req.City;
        if (req.State is not null) m.State = req.State;
        if (req.Pincode is not null) m.Pincode = req.Pincode;
        if (req.BankAccountNo is not null) m.BankAccountNo = req.BankAccountNo;
        if (req.BankIfsc is not null) m.BankIfsc = req.BankIfsc;
        if (req.BankHolderName is not null) m.BankHolderName = req.BankHolderName;

        await _db.SaveChangesAsync();

        // If identity fields changed AND member was APPROVED, demote to RE_KYC_REQUIRED.
        if (identityChanged && m.KycStatus == KycStatus.APPROVED)
        {
            _log.LogInformation("Identity fields changed for member {Mid}; demoting to RE_KYC_REQUIRED", memberId);
            await _kyc.ReviewAsync(memberId, KycStatus.RE_KYC_REQUIRED,
                "Auto: identity field updated after APPROVED");
        }
        return m;
    }

    public static MemberDto ToDto(Member m) => new(
        m.MemberId, m.TenantId, m.MemberType.ToString(),
        m.FullName, m.Phone, m.Email,m.PanNumber,
        m.AddressLine, m.City, m.State, m.Pincode,
        m.KycTier.ToString(), m.KycStatus.ToString(),
        m.KycApprovedAt, m.KycApprovedBy,
        m.BankAccountNo, m.BankIfsc, m.BankHolderName,
        m.CreatedAt
        );
         
}
