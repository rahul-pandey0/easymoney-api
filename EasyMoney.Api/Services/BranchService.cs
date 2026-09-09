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

    //Task<BranchDto?> GetByTenantAsync(long tenantId);
    Task<List<BranchDto>> GetByTenantAsync(long tenantId);

    Task<IReadOnlyList<BranchDto>> GetAllAsync();
    Task<BranchDto> UpdateAsync( long branchId,UpdateBranchRequest request);
    Task<BranchDto> UpdateBranchAsync(BranchDto b);
    Task<BranchDto> GetByIdAsync(long? branchId);
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
        long tenantId;

        if (_ctx.IsSifin)
        {
            tenantId = request.TenantId;
        }
        else
        {
            tenantId = _ctx.TenantId
                ?? throw new DomainException("Tenant not found");
        }


        var lastBranch = await _db.Branches .IgnoreQueryFilters()  .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.BranchId)  .FirstOrDefaultAsync();

        int sequence = 1;

        if (lastBranch != null)
        {
            sequence = int.Parse(lastBranch.BranchCode.Substring(3)) + 1;
        }

        var branchCode = $"{tenantId}{sequence:D3}";


        // Check duplicate
        var exists = await _db.Branches .AnyAsync(x =>x.TenantId == tenantId &&
                x.BranchCode == branchCode);

        if (exists)
            throw new DomainException($"Branch code '{branchCode}' already exists");


        var branch = new Branch
        {
            TenantId = tenantId,
            BranchCode = branchCode,   
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
            TransferGlId = request.TransferGlId,

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
            CreatedBy = _ctx.UserId,
            PreviousDate =request.PreviousDate,
            NextDate =request.NextDate,
            CurrentDate =request.CurrentDate

                ?? throw new DomainException("User is not authenticated")
        };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

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

    public async Task<BranchDto> GetByIdAsync(long? branchId)
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
    private static BranchDto ToDto(Branch branch)
    {
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
               branch.TransferGlId,
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
               branch.CreatedAt,
               branch.ModifiedAt,
               branch.ModifiedBy,
               branch.PreviousDate,
               branch.CurrentDate,
               branch.NextDate
        );
    }
    public async Task<BranchDto> UpdateAsync( long branchId, UpdateBranchRequest request)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(x => x.BranchId == branchId);

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
        branch. TransferGlId = request.TransferGlId;
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
        branch.ModifiedAt = DateTime.UtcNow;
        branch.ModifiedBy = _ctx.UserId;
        branch.PreviousDate = request.PreviousDate;
        branch.CurrentDate = request.CurrentDate;
        branch.NextDate = request.NextDate;

        await _db.SaveChangesAsync();

        return MapToBranchDto(branch);

    }


    public async Task<BranchDto> UpdateBranchAsync(BranchDto b) 
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(x => x.BranchId == b.BranchId);

        if (branch == null)
            throw new DomainException("Branch not found");

        if (!_ctx.IsSifin && branch.TenantId != _ctx.TenantId)
            throw new DomainException("Unauthorized access");
    
        branch.PreviousDate = b.PreviousDate;
        branch.CurrentDate = b.CurrentDate;
        branch.NextDate = b.NextDate;

        await _db.SaveChangesAsync();
        return MapToBranchDto(branch);
    }
    public async Task<List<BranchDto>> GetByTenantAsync(long tenantId)
    {
        Console.WriteLine($"Current TenantId: {_ctx.TenantId}");

        var branches = await _db.Branches
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)  // Use Where instead of FirstOrDefault
            .ToListAsync();

        if (branches == null || !branches.Any())
        {
            Console.WriteLine("No branches found");
            return new List<BranchDto>(); // Return empty list instead of null
        }

        Console.WriteLine($"Found {branches.Count} branches");

        return branches.Select(b => ToDto(b)).ToList();
    }
    private BranchDto MapToBranchDto(Branch branch)
    {
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
                    branch.TransferGlId,
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
                    branch.CreatedAt,
                    branch.ModifiedAt,
                    branch.ModifiedBy,
                    branch.PreviousDate,
                    branch.CurrentDate,
                    branch.NextDate
                 );

    }
}