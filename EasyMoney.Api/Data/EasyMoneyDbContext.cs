using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Data;

public class EasyMoneyDbContext : DbContext
{
    private readonly ITenantContext _ctx;

    public EasyMoneyDbContext(DbContextOptions<EasyMoneyDbContext> options, ITenantContext ctx)
        : base(options)
    {
        _ctx = ctx;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SchemeConfig> SchemeConfigs => Set<SchemeConfig>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<IndividualKycDetail> IndividualKycDetails => Set<IndividualKycDetail>();
    public DbSet<CorporateKycDetail> CorporateKycDetails => Set<CorporateKycDetail>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<KycReview> KycReviews => Set<KycReview>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BiddingCycle> BiddingCycles => Set<BiddingCycle>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Dividend> Dividends => Set<Dividend>();
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<GlAccountBalance> GlAccountBalances => Set<GlAccountBalance>();
    public DbSet<MemberAccountBalance> MemberAccountBalances => Set<MemberAccountBalance>();
    public DbSet<IdempotencyLog> IdempotencyLogs => Set<IdempotencyLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // The DB schema was created externally (via SQL file). EF Core treats it as
        // database-first: we map entities to existing tables/columns and never auto-migrate.

        b.Entity<Tenant>(e =>
        {
            e.ToTable("tenant");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.RegistrationNumber).HasColumnName("registration_number").HasMaxLength(100);
            e.Property(x => x.Address).HasColumnName("address").HasMaxLength(500);
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
            e.Property(x => x.OrgEmail).HasColumnName("org_email").HasMaxLength(150);
            e.Property(x => x.ContactPersonName).HasColumnName("contact_person_name").HasMaxLength(150);
            e.Property(x => x.ContactPersonPhone).HasColumnName("contact_person_phone").HasMaxLength(30);
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
        });

        b.Entity<SchemeConfig>(e =>
        {
            e.ToTable("scheme_config");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.TenureMonths).HasColumnName("tenure_months");
            e.Property(x => x.OrgFeePct).HasColumnName("org_fee_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.SifinCommissionPct).HasColumnName("sifin_commission_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.MinBidPct).HasColumnName("min_bid_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.MaxBidPct).HasColumnName("max_bid_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.EarlyExitPenaltyPct).HasColumnName("early_exit_penalty_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.MinInstallmentsForEligibility).HasColumnName("min_installments_for_eligibility");
            e.Property(x => x.BiddingWindowOpenDay).HasColumnName("bidding_window_open_day");
            e.Property(x => x.BiddingDayOfMonth).HasColumnName("bidding_day_of_month");
            e.Property(x => x.NoBidDefaultDividendPct).HasColumnName("no_bid_default_dividend_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.KycMode).HasColumnName("kyc_mode").HasConversion<string>();
            // The maker_checker_enabled flag will be added via a small migration script in Phase 2.
            // Mapped as a column for now; harmless if absent in DB until that script runs.
            e.Property(x => x.MakerCheckerEnabled).HasColumnName("maker_checker_enabled");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            e.HasOne(x => x.Tenant).WithOne(t => t.SchemeConfig).HasForeignKey<SchemeConfig>(x => x.TenantId);
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.Role).HasColumnName("role").HasConversion<string>();
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_token");
            e.HasKey(x => x.TokenId);
            e.Property(x => x.TokenId).HasColumnName("token_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<Member>(e =>
        {
            e.ToTable("member");
            e.HasKey(x => x.MemberId);
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.MemberType).HasColumnName("member_type").HasConversion<string>();
            e.Property(x => x.FullName).HasColumnName("full_name");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.AddressLine).HasColumnName("address_line");
            e.Property(x => x.City).HasColumnName("city");
            e.Property(x => x.State).HasColumnName("state");
            e.Property(x => x.Pincode).HasColumnName("pincode");
            e.Property(x => x.KycTier).HasColumnName("kyc_tier").HasConversion<string>();
            e.Property(x => x.KycStatus).HasColumnName("kyc_status").HasConversion<string>();
            e.Property(x => x.KycApprovedAt).HasColumnName("kyc_approved_at");
            e.Property(x => x.KycApprovedBy).HasColumnName("kyc_approved_by");
            e.Property(x => x.BankAccountNo).HasColumnName("bank_account_no");
            e.Property(x => x.BankIfsc).HasColumnName("bank_ifsc");
            e.Property(x => x.BankHolderName).HasColumnName("bank_holder_name");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<IndividualKycDetail>(e =>
        {
            e.ToTable("individual_kyc_detail");
            e.HasKey(x => x.MemberId);
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
            e.Property(x => x.Gender).HasColumnName("gender").HasConversion<string>();
            e.Property(x => x.FatherOrSpouseName).HasColumnName("father_or_spouse_name");
            e.Property(x => x.PanNumber).HasColumnName("pan_number");
            e.Property(x => x.AadhaarNumber).HasColumnName("aadhaar_number");
            e.Property(x => x.AadhaarLast4).HasColumnName("aadhaar_last4");
            e.Property(x => x.Occupation).HasColumnName("occupation");
            // The DB enum literals start with digits (e.g. '1L_5L'). C# enum names
            // cannot start with a digit, so we prefix with underscore and translate
            // both directions via a value converter.
            e.Property(x => x.AnnualIncomeBand).HasColumnName("annual_income_band")
                .HasConversion(
                    v => v.HasValue ? IncomeBandToDb(v.Value) : null,
                    v => v == null ? (IncomeBand?)null : IncomeBandFromDb(v));
            e.Property(x => x.NomineeName).HasColumnName("nominee_name");
            e.Property(x => x.NomineeRelation).HasColumnName("nominee_relation");
            e.Property(x => x.NomineeDob).HasColumnName("nominee_dob");
            e.Property(x => x.PermanentAddressLine).HasColumnName("permanent_address_line");
            e.Property(x => x.PermanentCity).HasColumnName("permanent_city");
            e.Property(x => x.PermanentState).HasColumnName("permanent_state");
            e.Property(x => x.PermanentPincode).HasColumnName("permanent_pincode");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<CorporateKycDetail>(e =>
        {
            e.ToTable("corporate_kyc_detail");
            e.HasKey(x => x.MemberId);
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasConversion<string>();
            e.Property(x => x.CinOrRegistrationNo).HasColumnName("cin_or_registration_no");
            e.Property(x => x.PanNumber).HasColumnName("pan_number");
            e.Property(x => x.Gstin).HasColumnName("gstin");
            e.Property(x => x.DateOfIncorporation).HasColumnName("date_of_incorporation");
            e.Property(x => x.RegisteredAddressLine).HasColumnName("registered_address_line");
            e.Property(x => x.RegisteredCity).HasColumnName("registered_city");
            e.Property(x => x.RegisteredState).HasColumnName("registered_state");
            e.Property(x => x.RegisteredPincode).HasColumnName("registered_pincode");
            e.Property(x => x.AuthorizedSignatoryName).HasColumnName("authorized_signatory_name");
            e.Property(x => x.AuthorizedSignatoryDesignation).HasColumnName("authorized_signatory_designation");
            e.Property(x => x.AuthorizedSignatoryPan).HasColumnName("authorized_signatory_pan");
            e.Property(x => x.AuthorizedSignatoryAadhaarLast4).HasColumnName("authorized_signatory_aadhaar_last4");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<KycDocument>(e =>
        {
            e.ToTable("kyc_document");
            e.HasKey(x => x.DocumentId);
            e.Property(x => x.DocumentId).HasColumnName("document_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.DocType).HasColumnName("doc_type").HasConversion<string>();
            e.Property(x => x.DocNumber).HasColumnName("doc_number");
            e.Property(x => x.FilePath).HasColumnName("file_path");
            e.Property(x => x.FileHash).HasColumnName("file_hash");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.Property(x => x.VerifiedAt).HasColumnName("verified_at");
            e.Property(x => x.VerifiedBy).HasColumnName("verified_by");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<KycReview>(e =>
        {
            e.ToTable("kyc_review");
            e.HasKey(x => x.ReviewId);
            e.Property(x => x.ReviewId).HasColumnName("review_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.FromStatus).HasColumnName("from_status").HasConversion<string>();
            e.Property(x => x.ToStatus).HasColumnName("to_status").HasConversion<string>();
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            e.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<Account>(e =>
        {
            e.ToTable("account");
            e.HasKey(x => x.AccountId);
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.AccountNumber).HasColumnName("account_number");
            e.Property(x => x.MonthlyContribution).HasColumnName("monthly_contribution").HasColumnType("decimal(12,2)");
            e.Property(x => x.AccountOpenDate).HasColumnName("account_open_date");
            e.Property(x => x.TenureEndDate).HasColumnName("tenure_end_date");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.InstallmentsPaid).HasColumnName("installments_paid");
            e.Property(x => x.IsPrized).HasColumnName("is_prized");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<BiddingCycle>(e =>
        {
            e.ToTable("bidding_cycle");
            e.HasKey(x => x.CycleId);
            e.Property(x => x.CycleId).HasColumnName("cycle_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.CycleMonth).HasColumnName("cycle_month");
            e.Property(x => x.WindowOpenAt).HasColumnName("window_open_at");
            e.Property(x => x.WindowCloseAt).HasColumnName("window_close_at");
            e.Property(x => x.GrossCorpus).HasColumnName("gross_corpus").HasColumnType("decimal(14,2)");
            e.Property(x => x.OrgFeeAmount).HasColumnName("org_fee_amount").HasColumnType("decimal(14,2)");
            e.Property(x => x.BidPool).HasColumnName("bid_pool").HasColumnType("decimal(14,2)");
            e.Property(x => x.WinnerAccountId).HasColumnName("winner_account_id");
            e.Property(x => x.WinnerBidPct).HasColumnName("winner_bid_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.LoanDisbursed).HasColumnName("loan_disbursed").HasColumnType("decimal(14,2)");
            e.Property(x => x.DividendPool).HasColumnName("dividend_pool").HasColumnType("decimal(14,2)");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<Bid>(e =>
        {
            e.ToTable("bid");
            e.HasKey(x => x.BidId);
            e.Property(x => x.BidId).HasColumnName("bid_id");
            e.Property(x => x.CycleId).HasColumnName("cycle_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.BidPct).HasColumnName("bid_pct").HasColumnType("decimal(5,2)");
            e.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsWinner).HasColumnName("is_winner");
        });

        b.Entity<Loan>(e =>
        {
            e.ToTable("loan");
            e.HasKey(x => x.LoanId);
            e.Property(x => x.LoanId).HasColumnName("loan_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.CycleId).HasColumnName("cycle_id");
            e.Property(x => x.PrincipalAmount).HasColumnName("principal_amount").HasColumnType("decimal(14,2)");
            e.Property(x => x.DisbursedAt).HasColumnName("disbursed_at");
            e.Property(x => x.OutstandingBalance).HasColumnName("outstanding_balance").HasColumnType("decimal(14,2)");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.RepaidAt).HasColumnName("repaid_at");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.ToTable("ledger_entry");
            e.HasKey(x => x.EntryId);
            e.Property(x => x.EntryId).HasColumnName("entry_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.CycleId).HasColumnName("cycle_id");
            e.Property(x => x.LinkedEntryId).HasColumnName("linked_entry_id");
            e.Property(x => x.EntryType).HasColumnName("entry_type").HasConversion<string>();
            e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(14,2)");
            e.Property(x => x.EntryDate).HasColumnName("entry_date");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<Dividend>(e =>
        {
            e.ToTable("dividend");
            e.HasKey(x => x.DividendId);
            e.Property(x => x.DividendId).HasColumnName("dividend_id");
            e.Property(x => x.CycleId).HasColumnName("cycle_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(14,2)");
        });

        b.Entity<GlAccount>(e =>
        {
            e.ToTable("gl_account");
            e.HasKey(x => x.GlAccountId);
            e.Property(x => x.GlAccountId).HasColumnName("gl_account_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.AccountClass).HasColumnName("account_class").HasConversion<string>();
            e.Property(x => x.ParentCode).HasColumnName("parent_code");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<JournalEntry>(e =>
        {
            e.ToTable("journal_entry");
            e.HasKey(x => x.JournalId);
            e.Property(x => x.JournalId).HasColumnName("journal_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.EntryDate).HasColumnName("entry_date");
            e.Property(x => x.SourceType).HasColumnName("source_type").HasConversion<string>();
            e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            e.HasMany(x => x.Lines).WithOne(x => x.Journal).HasForeignKey(x => x.JournalId);
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<JournalLine>(e =>
        {
            e.ToTable("journal_line");
            e.HasKey(x => x.LineId);
            e.Property(x => x.LineId).HasColumnName("line_id");
            e.Property(x => x.JournalId).HasColumnName("journal_id");
            e.Property(x => x.EntryTarget).HasColumnName("entry_target").HasConversion<string>();
            e.Property(x => x.GlAccountId).HasColumnName("gl_account_id");
            e.Property(x => x.MemberAccountId).HasColumnName("member_account_id");
            e.Property(x => x.Debit).HasColumnName("debit").HasColumnType("decimal(14,2)");
            e.Property(x => x.Credit).HasColumnName("credit").HasColumnType("decimal(14,2)");
            e.Property(x => x.RunningBalance).HasColumnName("running_balance").HasColumnType("decimal(14,2)");
            e.HasOne(x => x.GlAccount).WithMany().HasForeignKey(x => x.GlAccountId);
            e.HasOne(x => x.MemberAccount).WithMany().HasForeignKey(x => x.MemberAccountId);
            // Match the tenant filter from JournalEntry via the navigation. Journal is
            // marked required by FK; the filter mirrors JournalEntry's filter so EF doesn't
            // warn about "required end of relationship filtered out".
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.Journal.TenantId == _ctx.TenantId);
        });

        b.Entity<GlAccountBalance>(e =>
        {
            e.ToTable("gl_account_balance");
            e.HasKey(x => x.GlAccountId);
            e.Property(x => x.GlAccountId).HasColumnName("gl_account_id");
            e.Property(x => x.Balance).HasColumnName("balance").HasColumnType("decimal(16,2)");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<MemberAccountBalance>(e =>
        {
            e.ToTable("member_account_balance");
            e.HasKey(x => x.AccountId);
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Balance).HasColumnName("balance").HasColumnType("decimal(16,2)");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<IdempotencyLog>(e =>
        {
            e.ToTable("idempotency_log");
            e.HasKey(x => x.IdempotencyKey);
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Endpoint).HasColumnName("endpoint");
            e.Property(x => x.ResponseBody).HasColumnName("response_body").HasColumnType("json");
            e.Property(x => x.StatusCode).HasColumnName("status_code");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.AuditId).HasColumnName("audit_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Action).HasColumnName("action");
            e.Property(x => x.EntityType).HasColumnName("entity_type");
            e.Property(x => x.EntityId).HasColumnName("entity_id");
            e.Property(x => x.OldValue).HasColumnName("old_value").HasColumnType("json");
            e.Property(x => x.NewValue).HasColumnName("new_value").HasColumnType("json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        b.Entity<Notification>(e =>
        {
            e.ToTable("notification");
            e.HasKey(x => x.NotificationId);
            e.Property(x => x.NotificationId).HasColumnName("notification_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Body).HasColumnName("body");
            e.Property(x => x.IsRead).HasColumnName("is_read");
            e.Property(x => x.ReadAt).HasColumnName("read_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<ApprovalRequest>(e =>
        {
            e.ToTable("approval_request");
            e.HasKey(x => x.RequestId);
            e.Property(x => x.RequestId).HasColumnName("request_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.ActionType).HasColumnName("action_type").HasConversion<string>();
            e.Property(x => x.EntityType).HasColumnName("entity_type");
            e.Property(x => x.EntityId).HasColumnName("entity_id");
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("json");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.RequestedBy).HasColumnName("requested_by");
            e.Property(x => x.RequestedAt).HasColumnName("requested_at");
            e.Property(x => x.DecidedBy).HasColumnName("decided_by");
            e.Property(x => x.DecidedAt).HasColumnName("decided_at");
            e.Property(x => x.DecisionRemarks).HasColumnName("decision_remarks");
        });
    }

    private static string IncomeBandToDb(IncomeBand b) => b switch
    {
        IncomeBand.BELOW_1L => "BELOW_1L",
        IncomeBand._1L_5L => "1L_5L",
        IncomeBand._5L_10L => "5L_10L",
        IncomeBand._10L_25L => "10L_25L",
        IncomeBand.ABOVE_25L => "ABOVE_25L",
        _ => throw new ArgumentOutOfRangeException(nameof(b))
    };

    private static IncomeBand IncomeBandFromDb(string s) => s switch
    {
        "BELOW_1L" => IncomeBand.BELOW_1L,
        "1L_5L" => IncomeBand._1L_5L,
        "5L_10L" => IncomeBand._5L_10L,
        "10L_25L" => IncomeBand._10L_25L,
        "ABOVE_25L" => IncomeBand.ABOVE_25L,
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };
}
