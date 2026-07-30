using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IBranchService
{
    Task<BranchDto> CreateAsync(CreateBranchRequest request);
    Task<BranchDto?> GetByIdAsync(long branchId);
    Task<IReadOnlyList<BranchDto>> GetAllAsync();
    Task<BranchDto> UpdateAsync(
        long branchId,
        UpdateBranchRequest request);
}

public class BranchService : IBranchService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly ILogger<BranchService> _log;

    public BranchService(
        EasyMoneyDbContext db,
        ITenantContext ctx,
        ILogger<BranchService> log)
    {
        _db = db;
        _ctx = ctx;
        _log = log;
    }
    public async Task<BranchDto> CreateAsync(CreateBranchRequest request)
    {
        var tenantId = _ctx.TenantId
    ?? throw new DomainException("Tenant not found");


        var exists = await _db.Branches
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.BranchCode == request.BranchCode);

        if (exists)
            throw new DomainException($"Branch code '{request.BranchCode}' already exists");

        var branch = new Branch
        {
            TenantId = tenantId,

            BranchCode = request.BranchCode,
            BranchName = request.BranchName,
            BankId = request.BankId,

            RegistrationNo = request.RegistrationNo,
            RegistrationDate = request.RegistrationDate,
            BranchRegistrationDate = request.BranchRegistrationDate,

            Address = request.Address,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            ReferenceNo = request.ReferenceNo,

            CashGlId = request.CashGlId,
            AdjustmentGlId = request.AdjustmentGlId,

            BiddingDate = request.BiddingDate,
            CutoffDate = request.CutoffDate,
            BonusPaymentDate = request.BonusPaymentDate,

            Status = request.Status,

            MinimumRate = request.MinimumRate,
            MaximumRate = request.MaximumRate,
            Penalty = request.Penalty,

            DoublePaymentAllowed = request.DoublePaymentAllowed,

            MinimumInstallmentAmount = request.MinimumInstallmentAmount,
            MaximumInstallmentAmount = request.MaximumInstallmentAmount,
            MinimumIncrementAmount = request.MinimumIncrementAmount,

            OtherBank1 = request.OtherBank1,
            OtherBank2 = request.OtherBank2,

            CreatedAt = DateTime.UtcNow,
            CreatedBy = _ctx.UserId
            ?? throw new DomainException("User is not authenticated")
        };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        _log.LogInformation(
            "Branch {BranchCode} created for tenant {TenantId}",
            branch.BranchCode,
            branch.TenantId);

        return ToDto(branch);
    }
    public async Task<BranchDto?> GetByIdAsync(long branchId)
    {
        Console.WriteLine($"Current TenantId: {_ctx.TenantId}");

        var branch = await _db.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId);

        if (branch == null)
        {
            Console.WriteLine("Branch not found");
            return null;
        }

        Console.WriteLine($"Found Branch TenantId: {branch.TenantId}");

        return ToDto(branch);
    }
    public async Task<IReadOnlyList<BranchDto>> GetAllAsync()
    {
        var branches = await _db.Branches
            .AsNoTracking()
            .OrderBy(x => x.BranchName)
            .ToListAsync();

        Console.WriteLine($"Total branches from DB: {branches.Count}");

        return branches.Select(ToDto).ToList();
    }
    private static BranchDto ToDto(Branch x)
    {
        return new BranchDto(
            x.BranchId,
            x.TenantId,
            x.BranchCode,
            x.BranchName,
            x.BankId,
            x.RegistrationNo,
            x.RegistrationDate,
            x.BranchRegistrationDate,
            x.Address,
            x.PhoneNumber,
            x.Email,
            x.ReferenceNo,
            x.CashGlId,
            x.AdjustmentGlId,
            x.BiddingDate,
            x.CutoffDate,
            x.BonusPaymentDate,
            x.Status,
            x.MinimumRate,
            x.MaximumRate,
            x.Penalty,
            x.DoublePaymentAllowed,
            x.MinimumIncrementAmount,
            x.MinimumInstallmentAmount,
            x.MaximumInstallmentAmount,
            x.OtherBank1,
            x.OtherBank2,
            x.CreatedAt
        );
    }
    public async Task<BranchDto> UpdateAsync(
    long branchId,
    UpdateBranchRequest request)
    {
        var branch = await _db.Branches
            .FirstOrDefaultAsync(x => x.BranchId == branchId);

        if (branch == null)
            throw new DomainException("Branch not found");


        if (!_ctx.IsSifin && branch.TenantId != _ctx.TenantId)
            throw new DomainException("Unauthorized access");


        branch.BranchCode = request.BranchCode;
        branch.BranchName = request.BranchName;
        branch.BankId = request.BankId;

        branch.RegistrationNo = request.RegistrationNo;
        branch.RegistrationDate = request.RegistrationDate;
        branch.BranchRegistrationDate = request.BranchRegistrationDate;

        branch.Address = request.Address;
        branch.PhoneNumber = request.PhoneNumber;
        branch.Email = request.Email;

        branch.ReferenceNo = request.ReferenceNo;

        branch.CashGlId = request.CashGlId;
        branch.AdjustmentGlId = request.AdjustmentGlId;

        branch.BiddingDate = request.BiddingDate;
        branch.CutoffDate = request.CutoffDate;
        branch.BonusPaymentDate = request.BonusPaymentDate;

        branch.Status = request.Status;

        branch.MinimumRate = request.MinimumRate;
        branch.MaximumRate = request.MaximumRate;
        branch.Penalty = request.Penalty;

        branch.DoublePaymentAllowed = request.DoublePaymentAllowed;

        branch.MinimumIncrementAmount = request.MinimumIncrementAmount;
        branch.MinimumInstallmentAmount = request.MinimumInstallmentAmount;
        branch.MaximumInstallmentAmount = request.MaximumInstallmentAmount;

        branch.OtherBank1 = request.OtherBank1;
        branch.OtherBank2 = request.OtherBank2;

        //branch.UpdatedAt = DateTime.UtcNow;
        //branch.UpdatedBy = _ctx.UserId;


        await _db.SaveChangesAsync();


        return new BranchDto(
            branch.BranchId,
            branch.TenantId,
            branch.BranchCode,
            branch.BranchName,
            branch.BankId,
            branch.RegistrationNo,
            branch.RegistrationDate,
            branch.BranchRegistrationDate,
            branch.Address,
            branch.PhoneNumber,
            branch.Email,
            branch.ReferenceNo,
            branch.CashGlId,
            branch.AdjustmentGlId,
            branch.BiddingDate,
            branch.CutoffDate,
            branch.BonusPaymentDate,
            branch.Status,
            branch.MinimumRate,
            branch.MaximumRate,
            branch.Penalty,
            branch.DoublePaymentAllowed,
            branch.MinimumIncrementAmount,
            branch.MinimumInstallmentAmount,
            branch.MaximumInstallmentAmount,
            branch.OtherBank1,
            branch.OtherBank2,
            branch.CreatedAt
        );
    }
}