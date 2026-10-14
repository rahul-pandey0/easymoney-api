using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Configuration;
using MySql.Data.MySqlClient;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;


namespace EasyMoney.Api.Services;

public interface IMemberService
{
    Task<Member> CreateAsync(CreateMemberRequest req);
    Task<Member?> GetAsync(long memberId);
    Task<Member?> GetByAsync(string customerId);

    //Task<IReadOnlyList<MemberDto>> ListAsync(string? search, int skip, int take);
    Task<PagedResult<MemberDto>> ListAsync(string? search, int skip, int take);
    Task<Member> UpdateAsync(long memberId, UpdateMemberRequest req);
    Task<RelationMaster> CraeteMemberRelations(CreatememberRelationsRequest req);
    Task<RelationMaster?> GetRelationsAsync(long memberId);
    Task<List<RelationMaster>> GetByRelations();
    Task<RelationMaster> ApproveMemberAsync(long RelationId);

    Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidKycData(IFormFile file, string fileExtension, string batchCode);
    Task<(bool isSuccess, string errorMessage)> InsertToDatabase(string batchCode); 

}

public class MemberService : IMemberService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IKycService _kyc;
    private readonly ILogger<MemberService> _log;
    private readonly IConfiguration _configuration;


    public MemberService(EasyMoneyDbContext db, ITenantContext ctx, IKycService kyc, ILogger<MemberService> log, IConfiguration configuration)
    {
        _db = db; _ctx = ctx; _kyc = kyc; _log = log;
        _configuration = configuration;
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

        var tenantId = _ctx.TenantId.Value;
        var branchId = _ctx.BranchId.Value; // Rename local variable to avoid ambiguity  

        // Generate Customer Identifier Code  
        var lastMember = await _db.Members
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.MemberId)
            .FirstOrDefaultAsync();

        int sequence = 1;

        if (lastMember != null &&
            !string.IsNullOrEmpty(lastMember.CustomerIdentifierCode))
        {
            sequence = int.Parse(lastMember.CustomerIdentifierCode[^5..]) + 1;
        }

        var customerIdentifierCode = $"{tenantId}{sequence:D5}";

        var m = new Member
        {
            TenantId = tenantId, 
            BranchId = branchId, 

            CustomerIdentifierCode = customerIdentifierCode,

            MemberType = req.MemberType,
            FullName = req.FullName,
            Phone = req.Phone,
            Email = req.Email,
            PanNumber = req.PanNumber,
            IdType = req.IdType,
            IdNumber = req.IdNumber,
            AddressProof = req.AddressProof,
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

        _log.LogInformation(
            "Created member {Mid} with code {Code} (tenant {Tid}, type {Type})",
            m.MemberId,
            m.CustomerIdentifierCode,
            m.TenantId,
            m.MemberType);

        return m;
    }

    public Task<Member?> GetAsync(long memberId) =>
        _db.Members.FirstOrDefaultAsync(m => m.MemberId == memberId);

    public async Task<Member?> GetByAsync(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return null;

        try
        {
            // Use SingleOrDefaultAsync for better error detection
            var member = await _db.Members.SingleOrDefaultAsync(m =>
                m.CustomerIdentifierCode == customerId);

            return member;
        }
        catch (InvalidOperationException ex)
        {
          
            throw;
        }
    }

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
            .OrderByDescending(m => m.CreatedAt).Where(u => u.BranchId == _ctx.BranchId)
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

        m.BranchId=_ctx.BranchId; // Ensure the branch ID is set to the current context's branch ID
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
        m.MemberId, m.TenantId,m.BranchId, m.CustomerIdentifierCode,m.MemberType.ToString(),
        m.FullName, m.Phone, m.Email,m.PanNumber,m.IdType,m.IdNumber,m.AddressProof,
        m.AddressLine, m.City, m.State, m.Pincode,
        m.KycTier.ToString(), m.KycStatus.ToString(),
        m.KycApprovedAt, m.KycApprovedBy,
        m.BankAccountNo, m.BankIfsc, m.BankHolderName,
        m.CreatedAt
        );

    public async Task<RelationMaster> CraeteMemberRelations(CreatememberRelationsRequest req)
    {
        if (_ctx.TenantId is null)
            throw new DomainException("Tenant context required to create relation Member");

        var tenantId = _ctx.TenantId.Value;
        var branchId = _ctx.BranchId;

        var data = new RelationMaster
        {
            TenantId = tenantId,
            BranchId = branchId,
            MemberId = req.MemberId,
            MemberNo = req.MemberNo,
            Name = req.Name,
            PhoneNo = req.Phone,
            AccountNo = req.AccountNo,
            Remarks = req.Remarks,
            AuthStatus = "PENDING",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _ctx.UserId,
        
        };

        _db.RelationMaster.Add(data);
        await _db.SaveChangesAsync();

        // Create approval request
        var approval = new ApprovalRequest
        {
            TenantId = _ctx.TenantId.Value,
            ActionType = ApprovalActionType.RELATIONS,
            EntityType = "Relations Create",
            EntityId = data.MemberId,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                data.RelationId,
                data.MemberNo,
                data.AccountNo,
                data.AuthStatus,
                data.TenantId,
                data.CreatedBy,
                data.Name,
                data.PhoneNo,
            }),
            Status = ApprovalStatus.PENDING,
            RequestedBy = _ctx.UserId.Value,
            RequestedAt = DateTime.UtcNow
        };

        _db.ApprovalRequests.Add(approval);
        await _db.SaveChangesAsync();

        _log.LogInformation(
            "Created Realtions {Mid} with code {Code} (tenant {Tid}, type {Type})",
            data.MemberId,
            data.MemberNo,
            data.TenantId,
            data.Name);

        return data;
    }
    public Task<RelationMaster?> GetRelationsAsync(long memberId) => 
    _db.RelationMaster.FirstOrDefaultAsync(m => m.MemberId == memberId);






    public async Task<List<RelationMaster>> GetByRelations()
    {
        try
        {
            if (_ctx.TenantId is null)
                throw new DomainException("Tenant context required to get relations");

            var relations = await _db.RelationMaster
                .IgnoreQueryFilters()
                .Where(l => l.TenantId == _ctx.TenantId)
                .OrderByDescending(l => l.RelationId)
                .ToListAsync();
            return relations;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving relations for tenant {TenantId}", _ctx.TenantId);
            throw;
        }
    }


    public async Task<RelationMaster> ApproveMemberAsync(long RelationId) 
    {
        var loan = await _db.RelationMaster.IgnoreQueryFilters() 
            .FirstOrDefaultAsync(x => x.RelationId == RelationId)
            ?? throw new DomainException($"Loan {RelationId} not found."); 


        // Approve the loan
        loan.AuthStatus = "APPROVED";
        loan.ApprovedAt = DateTime.UtcNow;
        loan.ApprovedBy = _ctx.UserId;
        loan.BranchId = _ctx.BranchId;
         
        await _db.SaveChangesAsync();

        var approvalRequest = await _db.ApprovalRequests
            .FirstOrDefaultAsync(a => a.EntityType == "RELATIONS" && a.EntityId == RelationId && a.Status == ApprovalStatus.PENDING);
        if (approvalRequest != null)
        {
            approvalRequest.Status = ApprovalStatus.APPROVED;
            await _db.SaveChangesAsync();
        }


        _log.LogInformation("Relation approved by user {UserId}", RelationId, _ctx.UserId);
        return loan;
    }

    public async Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidKycData(IFormFile file, string fileExtension, string batchCode)
    {
        var modifiedRows = new List<string>();
        var rowCount = 0;

        try
        {
            using var stream = file.OpenReadStream();
            string[] columns;
            List<string[]> rows;

            if (fileExtension == ".csv")
                (columns, rows) = ParseCsv(stream);
            else if (fileExtension == ".xlsx")
                (columns, rows) = ParseExcel(stream);
            else
                throw new Exception("Unsupported file format.");

            // Remove blank rows, trim values
            rows = rows
                .Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(r => r.Select(v => v?.Trim() ?? "").ToArray())
                .ToList();

            rowCount = rows.Count;

            // Bulk insert into staging table
            var (inserted, error) = InsertRowsToStaging(rows, columns, batchCode);
            if (!inserted) return (false, rowCount, error);

            // Call MySQL validation function
            var (isValid, errCsv) = CallKycValidationFunction();
            if (!isValid)
                return (false, rowCount, errCsv);

            return (true, rowCount, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error processing KYC file");
            throw;
        }
    }
    private (bool, string) CallKycValidationFunction()
    {
        return (true, null);
    }

    public async Task<(bool isSuccess, string errorMessage)> InsertToDatabase(string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default");
        var connection = new MySqlConnection(connectionString);
        connection.Open();

        try
        {
            long? tenantId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;

            using (var cmd = new MySqlCommand("CALL upload_kyc_detail_data(@tenantId, @branchId)", connection))
            {
                cmd.Parameters.AddWithValue("@tenantId", tenantId);
                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.ExecuteNonQuery();
            }

          
            return (true, null);
        }

        catch (Exception ex)
        {
            _log.LogError(ex, "Error in InsertToDatabase");
            return (false, $"Unexpected error: {ex.Message}");
        }
    }


    private (string[] columns, List<string[]> rows) ParseCsv(Stream fileStream)
    {
        var rows = new List<string[]>();
        string[] columns;

        using (var reader = new StreamReader(fileStream))
        {
            var headerLine = reader.ReadLine();
            if (headerLine == null)
                throw new Exception("CSV file is empty.");

            columns = headerLine.Split(',');

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                rows.Add(line.Split(','));
            }
        }

        return (columns, rows);
    }




    // Resolve the ambiguity by using fully qualified names for ExcelPackage
    private (string[] columns, List<string[]> rows) ParseExcel(Stream fileStream)
    {
        var rows = new List<string[]>();
        ExcelPackage.License.SetNonCommercialPersonal("TechVerve");


        using (var package = new ExcelPackage(fileStream)) // Fully qualified name
        {
            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension.Rows;
            var colCount = worksheet.Dimension.Columns;

            var columns = new string[colCount];
            for (int col = 1; col <= colCount; col++)
            {
                columns[col - 1] = worksheet.Cells[1, col].Text;
            }

            for (int row = 2; row <= rowCount; row++)
            {
                var values = new string[colCount];
                for (int col = 1; col <= colCount; col++)
                {
                    values[col - 1] = worksheet.Cells[row, col].Text;
                }
                rows.Add(values);
            }

            return (columns, rows);
        }
    }

    //private (string[] columns, List<string[]> rows) ParseExcel(Stream fileStream)
    //{
    //    var rows = new List<string[]>();

    //    // ---------- 1. Validate magic bytes BEFORE EPPlus touches it ----------
    //    if (!fileStream.CanSeek)
    //        throw new Exception("Excel stream must be seekable.");

    //    long startPos = fileStream.Position;
    //    var sig = new byte[8];
    //    int read = fileStream.Read(sig, 0, 8);
    //    fileStream.Position = startPos;

    //    if (read < 4)
    //        throw new Exception("File is too small to be a valid Excel file.");

    //    bool isXlsx = sig[0] == 0x50 && sig[1] == 0x4B &&
    //                  sig[2] == 0x03 && sig[3] == 0x04;    // PK\x03\x04 (ZIP / OOXML)

    //    bool isXls = sig[0] == 0xD0 && sig[1] == 0xCF &&
    //                 sig[2] == 0x11 && sig[3] == 0xE0;     // old binary .xls

    //    if (isXls)
    //        throw new Exception(
    //            "File is an old .xls format. Please save it as .xlsx (Excel Workbook) and re-upload.");

    //    if (!isXlsx)
    //        throw new Exception(
    //            "File is not a valid .xlsx. It looks like a CSV or text file. " +
    //            "Either upload the original .csv, or open it in Excel and 'Save As' .xlsx.");

    //    // ---------- 2. Copy into MemoryStream for safe EPPlus reading ----------
    //    // EPPlus needs a seekable stream with position 0. Copying avoids partial-read issues.
    //    using var ms = new MemoryStream();
    //    fileStream.CopyTo(ms);
    //    ms.Position = 0;

    //    // ---------- 3. Parse with EPPlus ----------
    //    using var package = new OfficeOpenXml.ExcelPackage(ms);

    //    if (package.Workbook.Worksheets.Count == 0)
    //        throw new Exception("Excel file contains no worksheets.");

    //    var worksheet = package.Workbook.Worksheets[0];

    //    if (worksheet.Dimension == null)
    //        throw new Exception("Excel worksheet is empty (no data).");

    //    int rowCount = worksheet.Dimension.End.Row;
    //    int colCount = worksheet.Dimension.End.Column;

    //    // Header
    //    var columns = new string[colCount];
    //    for (int col = 1; col <= colCount; col++)
    //    {
    //        columns[col - 1] = worksheet.Cells[1, col].Value?.ToString()?.Trim() ?? string.Empty;
    //    }

    //    // Data rows
    //    for (int row = 2; row <= rowCount; row++)
    //    {
    //        var values = new string[colCount];
    //        for (int col = 1; col <= colCount; col++)
    //        {
    //            values[col - 1] = worksheet.Cells[row, col].Value?.ToString() ?? string.Empty;
    //        }
    //        rows.Add(values);
    //    }

    //    return (columns, rows);
    //}
    private (bool, string) InsertRowsToStaging(
       List<string[]> rows, string[] columns, string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default");

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();

        try
        {
            // 1) TRUNCATE staging table
            //using (var cmd = new MySqlCommand("TRUNCATE TABLE upload_kyc_detail", connection, tx))
            //    cmd.ExecuteNonQuery();

            // 2) Bulk insert
            long? bankId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;
            var colNames = string.Join(", ", columns.Select(c => $"`{c}`")) + ", `batch_code`";
            var paramPlaceholders = string.Join(", ",
                Enumerable.Range(0, columns.Length + 1).Select(i => $"@p{i}"));
            var del = $"DELETE FROM upload_kyc_detail WHERE branch_id = {branchId} AND tenant_id = {bankId}";
            var insertSql =
                $"INSERT INTO upload_kyc_detail ({colNames}) VALUES ({paramPlaceholders})";

            foreach (var row in rows)
            {
                using var cmd = new MySqlCommand(insertSql, connection, tx);
                for (int i = 0; i < columns.Length; i++)
                {
                    cmd.Parameters.AddWithValue($"@p{i}",
                        i < row.Length ? (object?)row[i] ?? DBNull.Value : DBNull.Value);
                }
                cmd.Parameters.AddWithValue($"@p{columns.Length}", batchCode);
                cmd.ExecuteNonQuery();
            }

            // 3) Call validation PROCEDURE (not function) — ExecuteNonQuery

            using (var cmd = new MySqlCommand("CALL validate_upload_kyc(@bankId,@branchId)", connection, tx))
            {
                cmd.Parameters.AddWithValue("@bankId", bankId);
                cmd.Parameters.AddWithValue("@branchId", branchId);

                cmd.ExecuteNonQuery();
            }

            // 4) Count errors in a SEPARATE command
            int errorCount;
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM upload_kyc_detail WHERE error = 1", connection, tx))
            {
                errorCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (errorCount > 0)
            {
                // 5) Fetch error rows — reader scoped in its own using block
                var output = new List<string>();

                using (var errCmd = new MySqlCommand(
                    "SELECT * FROM upload_kyc_detail WHERE error = 1", connection, tx))
                using (var reader = errCmd.ExecuteReader())
                {
                    // Build list of column indices to include
                    var include = new List<int>();
                    var headerNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        if (name is "batch_code" or "error") continue;
                        include.Add(i);
                        headerNames.Add(name);
                    }
                    output.Add(string.Join(",", headerNames));

                    while (reader.Read())
                    {
                        var vals = include
                            .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString())
                            .Select(v => v!.Replace(",", " ")); // avoid breaking CSV columns
                        output.Add(string.Join(",", vals));
                    }
                } // ← reader disposed here, connection free again

                tx.Rollback();
                return (false, string.Join(Environment.NewLine, output));
            }

            // 6) Optional — record batch master (uncomment when needed)
            // using (var cmd = new MySqlCommand(
            //     @"INSERT INTO batch_master (module, batch_code, tenant_id, branch_id, status)
            //       VALUES ('KYC', @batch, @tenant, @branch, 'VALIDATED')", connection, tx))
            // {
            //     cmd.Parameters.AddWithValue("@batch", batchCode);
            //     cmd.Parameters.AddWithValue("@tenant", _ctx.TenantId);
            //     cmd.Parameters.AddWithValue("@branch", _ctx.BranchId);
            //     cmd.ExecuteNonQuery();
            // }

            tx.Commit();
            return (true, null!);
        }
        catch (MySqlException ex)
        {
            try { tx.Rollback(); } catch { /* ignore */ }
            _log.LogError(ex, "MySQL error in InsertRowsToStaging");
            return (false, ex.Message);
        }
    }



}
