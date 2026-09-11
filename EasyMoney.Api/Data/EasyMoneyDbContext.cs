using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using static Dapper.SqlMapper;

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
    public DbSet<SchemeMaster> SchemeMaster => Set<SchemeMaster>();
    public DbSet<GeneralLedgerMaster> GeneralLedgerMaster => Set<GeneralLedgerMaster>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<RelationMaster> RelationMaster => Set<RelationMaster>(); 
    public DbSet<IndividualKycDetail> IndividualKycDetails => Set<IndividualKycDetail>();
    public DbSet<CorporateKycDetail> CorporateKycDetails => Set<CorporateKycDetail>();
    public DbSet<RegisteredOffice> RegisteredOffices => Set<RegisteredOffice>();
    public DbSet<CorporateContactDetail> CorporateContactDetails => Set<CorporateContactDetail>();
    public DbSet<CorporateDirector> CorporateDirectors => Set<CorporateDirector>();
    public DbSet<CorporateBeneficialOwner> CorporateBeneficialOwners => Set<CorporateBeneficialOwner>();
    public DbSet<CorporateAuthorizedSignatory> CorporateAuthorizedSignatories => Set<CorporateAuthorizedSignatory>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<KycReview> KycReviews => Set<KycReview>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountClosure> AccountClosure => Set<AccountClosure>();

    public DbSet<BiddingCycle> BiddingCycles => Set<BiddingCycle>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<CoBorrower> CoBorrowers => Set<CoBorrower>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Dividend> Dividends => Set<Dividend>();
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<GlAccountBalance> GlAccountBalances => Set<GlAccountBalance>();
    public DbSet<GLAccountDailyBalance> GLAccountDailyBalance => Set<GLAccountDailyBalance>();

    public DbSet<MemberAccountBalance> MemberAccountBalances => Set<MemberAccountBalance>();
    public DbSet<IdempotencyLog> IdempotencyLogs => Set<IdempotencyLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<PaymentDetail> PaymentDetail => Set<PaymentDetail>();
    public DbSet<BonusDetail> BonusDetails => Set<BonusDetail>(); 
    public DbSet<Bonus> Bonus => Set<Bonus>();  
    public DbSet<BonusDistribution> BonusDistribution => Set<BonusDistribution>(); 
    public DbSet<SifinCommission> SifinCommission => Set<SifinCommission>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();



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
            e.Property(x => x.AuthorisationRequired).HasColumnName("authorisation_required");
            e.Property(x => x.EmailNotification).HasColumnName("email_notification");
            e.Property(x => x.SmsNotification).HasColumnName("sms_notification");
            e.Property(x => x.LogoData).HasColumnName("logo_data");
            e.Property(x => x.LogoContentType).HasColumnName("logo_content_type").HasMaxLength(100);
            e.Property(x => x.LogoFileName).HasColumnName("logo_file_name").HasMaxLength(255);

        });

        b.Entity<SchemeConfig>(e =>
        {
            e.ToTable("scheme_config");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.SchemeId).HasColumnName("scheme_id");

            e.Property(x => x.SchemeName).HasColumnName("scheme_name");
            e.Property(x => x.FixedRate).HasColumnName("fixed_rate").HasColumnType("decimal(18,2)");
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
            // Bank Details
            e.Property(x => x.BankName).HasColumnName("bank_name");
            e.Property(x => x.CustAddress1).HasColumnName("cust_address1");
            e.Property(x => x.CustAddress2).HasColumnName("cust_address2");
            e.Property(x => x.CustAddress3).HasColumnName("cust_address3");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PhNum).HasColumnName("ph_num");
            //e.Property(x => x.Fax).HasColumnName("fax");
            e.Property(x => x.RdStatus).HasColumnName("rd_status");

            // Bonus / Commission
            e.Property(x => x.GrossBonus)
                .HasColumnName("gross_bonus")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.TenantCommission)
                .HasColumnName("tenant_commission")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.NetBonus)
                .HasColumnName("net_bonus")
                .HasColumnType("decimal(18,2)");

            // Reserve / GL Accounts
            e.Property(x => x.Reserve1).HasColumnName("reserve1");
            e.Property(x => x.Reserve2).HasColumnName("reserve2");
            e.Property(x => x.PoolMoney).HasColumnName("pool_money");
            e.Property(x => x.TenantPin).HasColumnName("tenant_pin");
            e.Property(x => x.LoanAssetGL).HasColumnName("loan_asset_gl");
            e.Property(x => x.SifinPayable).HasColumnName("sifin_payable");

            // Time Change
            e.Property(x => x.TimeChPass)
                .HasColumnName("time_ch_pass")
                .HasColumnType("decimal(18,2)");

            // Penalty Accounts
            e.Property(x => x.PenaltyAcc).HasColumnName("penalty_acc");
            e.Property(x => x.NMPenaltyAcc).HasColumnName("nm_penalty_acc");

            // Interest Configuration
            e.Property(x => x.MinimumRate)
                .HasColumnName("minimum_rate")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.MaximumRate)
                .HasColumnName("maximum_rate")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.MinimumPeriod).HasColumnName("minimum_period");
            e.Property(x => x.MaximumPeriod).HasColumnName("maximum_period");

            // Tax
            e.Property(x => x.TdsAc).HasColumnName("tds_ac");
            e.Property(x => x.GstGl).HasColumnName("gst_gl");
            e.Property(x => x.ServicesTax).HasColumnName("services_tax");
            e.Property(x => x.SchemeCode).HasColumnName("scheme_code");
            e.Property(x => x.OrgFeeGlId).HasColumnName("org_fee_gl_id");
            e.Property(x => x.SifinCommissionGlId).HasColumnName("sifin_commission_gl_id");

            e.Property(x => x.MinimumInstallmentAmount).HasColumnName("minimum_installment_amount");
            e.Property(x => x.MaximumInstallmentAmount).HasColumnName("maximum_installment_amount");

            e.Property(x => x.BonusGl).HasColumnName("bonus_gl");
            e.Property(x => x.BonusGlId).HasColumnName("bonus_gl_id");
        });

        b.Entity<SchemeMaster>(e =>
        {
            e.ToTable("scheme_master");
            e.HasKey(x => x.SchemeId);
            e.Property(x => x.SchemeName).HasColumnName("scheme_name");
            e.Property(x => x.FixedRate).HasColumnName("fixed_rate").HasColumnType("decimal(18,2)");
            e.Property(x => x.SchemeId).HasColumnName("scheme_id");
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
            //e.HasOne(x => x.Tenant).WithOne(t => t.SchemeConfig).HasForeignKey<SchemeConfig>(x => x.TenantId);
            // Bank Details
            e.Property(x => x.BankName).HasColumnName("bank_name");
            e.Property(x => x.CustAddress1).HasColumnName("cust_address1");
            e.Property(x => x.CustAddress2).HasColumnName("cust_address2");
            e.Property(x => x.CustAddress3).HasColumnName("cust_address3");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PhNum).HasColumnName("ph_num");
            //e.Property(x => x.Fax).HasColumnName("fax");
            e.Property(x => x.RdStatus).HasColumnName("rd_status");

            // Bonus / Commission
            e.Property(x => x.GrossBonus)
                .HasColumnName("gross_bonus")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.TenantCommission)
                .HasColumnName("tenant_commission")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.NetBonus)
                .HasColumnName("net_bonus")
                .HasColumnType("decimal(18,2)");

            // Reserve / GL Accounts
            e.Property(x => x.Reserve1).HasColumnName("reserve1");
            e.Property(x => x.Reserve2).HasColumnName("reserve2");
            e.Property(x => x.PoolMoney).HasColumnName("pool_money");
            e.Property(x => x.TenantPin).HasColumnName("tenant_pin");
            e.Property(x => x.LoanAssetGL).HasColumnName("loan_asset_gl");
            e.Property(x => x.SifinPayable).HasColumnName("sifin_payable");

            // Time Change
            e.Property(x => x.TimeChPass)
                .HasColumnName("time_ch_pass")
                .HasColumnType("decimal(18,2)");

            // Penalty Accounts
            e.Property(x => x.PenaltyAcc).HasColumnName("penalty_acc");
            e.Property(x => x.NMPenaltyAcc).HasColumnName("nm_penalty_acc");

            // Interest Configuration
            e.Property(x => x.MinimumRate)
                .HasColumnName("minimum_rate")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.MaximumRate)
                .HasColumnName("maximum_rate")
                .HasColumnType("decimal(18,2)");

            e.Property(x => x.MinimumPeriod).HasColumnName("minimum_period");
            e.Property(x => x.MaximumPeriod).HasColumnName("maximum_period");

            // Tax
            e.Property(x => x.TdsAc).HasColumnName("tds_ac");
            e.Property(x => x.GstGl).HasColumnName("gst_gl");
            e.Property(x => x.SchemeCode).HasColumnName("scheme_code");

            e.Property(x => x.ServicesTax).HasColumnName("services_tax");


            e.Property(x => x.MinimumInstallmentAmount).HasColumnName("minimum_installment_amount");
            e.Property(x => x.MaximumInstallmentAmount).HasColumnName("maximum_installment_amount");


        });


        b.Entity<GeneralLedgerMaster>(e =>
        {
            e.ToTable("general_ledger_master");
            e.HasKey(x => x.GlId);
            e.Property(x => x.GlId).HasColumnName("gl_id");
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Forbank).HasColumnName("for_bank");
            e.Property(x => x.Category).HasColumnName("category").HasConversion<string>();
            e.Property(x => x.IsReported).HasColumnName("is_reported");
            e.Property(x => x.HasTransactions).HasColumnName("has_transactions");
            e.Property(x => x.HasGst).HasColumnName("has_gst");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.ParentGl).HasColumnName("parent_gl");
            e.Property(x => x.status).HasColumnName("status");
        });

        b.Entity<AppUser>(e =>
            {
                e.ToTable("app_user");
                e.HasKey(x => x.UserId);
                e.Property(x => x.UserId).HasColumnName("user_id");
                e.Property(x => x.TenantId).HasColumnName("tenant_id");
                e.Property(x => x.MemberId).HasColumnName("member_id");
                e.Property(x => x.UserName).HasColumnName("user_name");
                e.Property(x => x.BranchId).HasColumnName("branch_id");
                e.Property(x => x.Email).HasColumnName("email");
                e.Property(x => x.PasswordHash).HasColumnName("password_hash");
                e.Property(x => x.Role).HasColumnName("role").HasConversion<string>();
                e.Property(x => x.CreatedBy).HasColumnName("created_by");
                e.Property(x => x.IsActive).HasColumnName("is_active");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
                e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
                e.Property(x => x.PasswordChangedAt).HasColumnName("password_changed_at");
                e.Property(x => x.UpdatedAt).HasColumnName("update_at");
                e.Property(x => x.HintAnswerHash).HasColumnName("hint_answer_hash");
                e.Property(x => x.HintQuestion).HasColumnName("hint_question");

                e.HasOne(u => u.Branch).WithMany().HasForeignKey(u => u.BranchId).OnDelete(DeleteBehavior.Restrict);
                b.Entity<AppUser>()
        .HasOne(u => u.Member)
        .WithMany()
        .HasForeignKey(u => u.MemberId);

                b.Entity<AppUser>()
                    .HasOne(u => u.Tenant)
                    .WithMany()
                    .HasForeignKey(u => u.TenantId);
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
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.CustomerIdentifierCode).HasColumnName("customer_identifier_code").HasMaxLength(20).IsRequired();
            e.Property(x => x.MemberType).HasColumnName("member_type").HasConversion<string>();
            e.Property(x => x.FullName).HasColumnName("full_name");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PanNumber).HasColumnName("pan_number").HasMaxLength(10);
            e.Property(x => x.IdType).HasColumnName("id_type");
            e.Property(x => x.IdNumber).HasColumnName("id_number");
            e.Property(x => x.AddressProof).HasColumnName("address_proof");
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

        //b.Entity<IndividualKycDetail>(e =>
        //{
        //    e.ToTable("individual_kyc_detail");
        //    e.HasKey(x => x.MemberId);
        //    e.Property(x => x.MemberId).HasColumnName("member_id");
        //    e.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
        //    e.Property(x => x.Gender).HasColumnName("gender").HasConversion<string>();
        //    e.Property(x => x.FatherOrSpouseName).HasColumnName("father_or_spouse_name");
        //    e.Property(x => x.PanNumber).HasColumnName("pan_number");
        //    e.Property(x => x.AadhaarNumber).HasColumnName("aadhaar_number");
        //    e.Property(x => x.AadhaarLast4).HasColumnName("aadhaar_last4");
        //    e.Property(x => x.Occupation).HasColumnName("occupation");
        //    // The DB enum literals start with digits (e.g. '1L_5L'). C# enum names
        //    // cannot start with a digit, so we prefix with underscore and translate
        //    // both directions via a value converter.
        //    e.Property(x => x.AnnualIncomeBand).HasColumnName("annual_income_band")
        //        .HasConversion(
        //            v => v.HasValue ? IncomeBandToDb(v.Value) : null,
        //            v => v == null ? (IncomeBand?)null : IncomeBandFromDb(v));
        //    e.Property(x => x.NomineeName).HasColumnName("nominee_name");
        //    e.Property(x => x.NomineeRelation).HasColumnName("nominee_relation");
        //    e.Property(x => x.NomineeDob).HasColumnName("nominee_dob");
        //    e.Property(x => x.PermanentAddressLine).HasColumnName("permanent_address_line");
        //    e.Property(x => x.PermanentCity).HasColumnName("permanent_city");
        //    e.Property(x => x.PermanentState).HasColumnName("permanent_state");
        //    e.Property(x => x.PermanentPincode).HasColumnName("permanent_pincode");
        //    e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        //});

        b.Entity<IndividualKycDetail>(e =>
        {
            e.ToTable("individual_kyc_detail");
            e.HasKey(x => x.MemberId);
            e.Property(x => x.MemberId).HasColumnName("member_id");
            // Personal Details
            e.Property(x => x.BranchId).HasColumnName("branch_id");

            e.Property(x => x.CustomerId).HasColumnName("customer_id").HasMaxLength(45);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.MobileNumber).HasColumnName("mobile_number").HasMaxLength(20);
            e.Property(x => x.ResidencePhone).HasColumnName("residence_phone").HasMaxLength(20);
            e.Property(x => x.OfficePhone).HasColumnName("office_phone").HasMaxLength(20);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100);
            e.Property(x => x.Age).HasColumnName("age");
            e.Property(x => x.Education).HasColumnName("education").HasMaxLength(100);
            e.Property(x => x.MaritalStatus).HasColumnName("marital_status").HasMaxLength(30);

            // KYC Details
            e.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
            e.Property(x => x.Gender).HasColumnName("gender").HasConversion<string>();
            e.Property(x => x.FatherOrSpouseName).HasColumnName("father_or_spouse_name");
            e.Property(x => x.PanNumber).HasColumnName("pan_number");
            e.Property(x => x.AadhaarNumber).HasColumnName("aadhaar_number");
            e.Property(x => x.AadhaarLast4).HasColumnName("aadhaar_last4");
            e.Property(x => x.Occupation).HasColumnName("occupation");

            e.Property(x => x.AnnualIncomeBand).HasColumnName("annual_income_band").HasConversion(
                    v => v.HasValue ? IncomeBandToDb(v.Value) : null,
                    v => v == null ? (IncomeBand?)null : IncomeBandFromDb(v));

            e.Property(x => x.NomineeName).HasColumnName("nominee_name");
            e.Property(x => x.NomineeRelation).HasColumnName("nominee_relation");
            e.Property(x => x.NomineeDob).HasColumnName("nominee_dob");

            e.Property(x => x.PermanentAddressLine).HasColumnName("permanent_address_line");
            e.Property(x => x.PermanentCity).HasColumnName("permanent_city");
            e.Property(x => x.PermanentState).HasColumnName("permanent_state");
            e.Property(x => x.PermanentPincode).HasColumnName("permanent_pincode");

            // ID Details
            e.Property(x => x.IdType).HasColumnName("id_type").HasMaxLength(50);
            e.Property(x => x.IdNumber).HasColumnName("id_number").HasMaxLength(100);
            e.Property(x => x.AddressProof).HasColumnName("address_proof").HasMaxLength(100);
            e.Property(x => x.DocumentNumber).HasColumnName("document_number").HasMaxLength(100);
            e.Property(x => x.CustomerImage).HasColumnName("customer_image").HasMaxLength(255);
            e.Property(x => x.IdImage).HasColumnName("id_image").HasMaxLength(255);
            e.Property(x => x.DocumentImage).HasColumnName("document_image").HasMaxLength(255);

            // Bank Details
            e.Property(x => x.BankName).HasColumnName("bank_name").HasMaxLength(100);
            e.Property(x => x.SavingsAccountNumber).HasColumnName("savings_account_number").HasMaxLength(50);
            e.Property(x => x.CurrentAccountNumber).HasColumnName("current_account_number").HasMaxLength(50);

            // Family Details
            e.Property(x => x.TotalFamilyMembers).HasColumnName("total_family_members");
            e.Property(x => x.DependentFamilyMembers).HasColumnName("dependent_family_members");
            e.Property(x => x.EarningFamilyMembers).HasColumnName("earning_family_members");
            e.Property(x => x.AdditionalPersonalDetails).HasColumnName("additional_personal_details");
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.Remarks1).HasColumnName("remarks1");

            // Vehicle Details
            e.Property(x => x.BikeModel).HasColumnName("bike_model").HasMaxLength(100);
            e.Property(x => x.BikeCompany).HasColumnName("bike_company").HasMaxLength(100);
            e.Property(x => x.CarModel).HasColumnName("car_model").HasMaxLength(100);
            e.Property(x => x.CarCompany).HasColumnName("car_company").HasMaxLength(100);
            e.Property(x => x.TractorModel).HasColumnName("tractor_model").HasMaxLength(100);
            e.Property(x => x.TractorCompany).HasColumnName("tractor_company").HasMaxLength(100);
            e.Property(x => x.HeavyVehicleModel).HasColumnName("heavy_vehicle_model").HasMaxLength(100);
            e.Property(x => x.HeavyVehicleCompany).HasColumnName("heavy_vehicle_company").HasMaxLength(100);

            // Member Details
            e.Property(x => x.MembershipNumber)
                .HasColumnName("membership_number")
                .HasMaxLength(50);

            e.Property(x => x.AccountType)
                .HasColumnName("account_type")
                .HasMaxLength(50);

            e.Property(x => x.AccountNumber)
                .HasColumnName("account_number")
                .HasMaxLength(50);


            // Property Details
            e.Property(x => x.AgricultureLand).HasColumnName("agriculture_land").HasMaxLength(255);
            e.Property(x => x.AgricultureArea).HasColumnName("agriculture_area").HasColumnType("decimal(18,2)");
            e.Property(x => x.AgricultureSurveyNo).HasColumnName("agriculture_survey_no").HasMaxLength(100);
            e.Property(x => x.AgricultureValue).HasColumnName("agriculture_value").HasColumnType("decimal(18,2)");

            e.Property(x => x.SiteDetails).HasColumnName("site_details");
            e.Property(x => x.SiteArea).HasColumnName("site_area").HasColumnType("decimal(18,2)");
            e.Property(x => x.SiteSurveyNo).HasColumnName("site_survey_no").HasMaxLength(100);
            e.Property(x => x.SiteValue).HasColumnName("site_value").HasColumnType("decimal(18,2)");

            e.Property(x => x.PlantationDetails).HasColumnName("plantation_details");
            e.Property(x => x.PlantationArea).HasColumnName("plantation_area").HasColumnType("decimal(18,2)");
            e.Property(x => x.PlantationSurveyNo).HasColumnName("plantation_survey_no").HasMaxLength(100);
            e.Property(x => x.PlantationValue).HasColumnName("plantation_value").HasColumnType("decimal(18,2)");

            e.Property(x => x.HouseDetails).HasColumnName("house_details");
            e.Property(x => x.HouseArea).HasColumnName("house_area").HasColumnType("decimal(18,2)");
            e.Property(x => x.HouseNumber).HasColumnName("house_number").HasMaxLength(100);
            e.Property(x => x.HouseValue).HasColumnName("house_value").HasColumnType("decimal(18,2)");

            // Occupational Details
            e.Property(x => x.EmploymentNature).HasColumnName("employment_nature").HasMaxLength(100);
            e.Property(x => x.EmployerName).HasColumnName("employer_name").HasMaxLength(150);
            e.Property(x => x.SalaryDetails).HasColumnName("salary_details").HasColumnType("decimal(18,2)");
            e.Property(x => x.Designation).HasColumnName("designation").HasMaxLength(100);
            e.Property(x => x.OrganizationNature).HasColumnName("organization_nature").HasMaxLength(100);
            e.Property(x => x.Department).HasColumnName("department").HasMaxLength(100);
            e.Property(x => x.OfficeAddress).HasColumnName("office_address");

            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<CorporateKycDetail>(e =>
        {
            e.ToTable("corporate_kyc_detail");

            e.HasKey(x => x.MemberId);
            e.Property(x => x.BranchId).HasColumnName("branch_id");

            e.Property(x => x.MemberId).HasColumnName("member_id");

            e.Property(x => x.EntityName).HasColumnName("entity_name").HasMaxLength(200);

            e.Property(x => x.EntityType)
                .HasColumnName("entity_type")
                .HasConversion<string>();

            e.Property(x => x.CinOrRegistrationNo)
                .HasColumnName("cin_or_registration_no")
                .HasMaxLength(100);

            e.Property(x => x.PanNumber)
                .HasColumnName("pan_number")
                .HasMaxLength(20);

            e.Property(x => x.Gstin)
                .HasColumnName("gstin")
                .HasMaxLength(20);

            e.Property(x => x.DateOfIncorporation)
                .HasColumnName("date_of_incorporation");

            e.Property(x => x.PlaceOfIncorporation)
                .HasColumnName("place_of_incorporation")
                .HasMaxLength(100);

            e.Property(x => x.CountryOfIncorporation)
                .HasColumnName("country_of_incorporation")
                .HasMaxLength(100);

            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");

            e.HasOne(x => x.RegisteredOffice)
                .WithOne(x => x.CorporateKycDetail)
                .HasForeignKey<RegisteredOffice>(x => x.MemberId);

            e.HasMany(x => x.ContactDetails)
                .WithOne(x => x.CorporateKycDetail)
                .HasForeignKey(x => x.MemberId);

            e.HasMany(x => x.Directors)
                .WithOne(x => x.CorporateKycDetail)
                .HasForeignKey(x => x.MemberId);

            e.HasMany(x => x.BeneficialOwners)
                .WithOne(x => x.CorporateKycDetail)
                .HasForeignKey(x => x.MemberId);

            e.HasMany(x => x.AuthorizedSignatories)
                .WithOne(x => x.CorporateKycDetail)
                .HasForeignKey(x => x.MemberId);
        });

        b.Entity<RegisteredOffice>(e =>
        {
            e.ToTable("corporate_registered_office");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.MemberId).HasColumnName("member_id");

            e.Property(x => x.AddressLine1).HasColumnName("address_line1");
            e.Property(x => x.AddressLine2).HasColumnName("address_line2");
            e.Property(x => x.AddressLine3).HasColumnName("address_line3");

            e.Property(x => x.City).HasColumnName("city");
            e.Property(x => x.State).HasColumnName("state");
            e.Property(x => x.Pincode).HasColumnName("pincode");
            e.Property(x => x.Country).HasColumnName("country");
        });
        b.Entity<CorporateContactDetail>(e =>
        {
            e.ToTable("corporate_contact_detail");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.MemberId).HasColumnName("member_id");

            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Website).HasColumnName("website");
        });
        b.Entity<CorporateDirector>(e =>
        {
            e.ToTable("corporate_director");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.MemberId).HasColumnName("member_id");

            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Designation).HasColumnName("designation");
            e.Property(x => x.Din).HasColumnName("din");
            e.Property(x => x.Pan).HasColumnName("pan");
            e.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
        });
        b.Entity<CorporateBeneficialOwner>(e =>
        {
            e.ToTable("corporate_beneficial_owner");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.MemberId).HasColumnName("member_id");

            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.OwnershipPercentage).HasColumnName("ownership_percentage");
            e.Property(x => x.Pan).HasColumnName("pan");
            e.Property(x => x.Din).HasColumnName("din");
            e.Property(x => x.Nationality).HasColumnName("nationality");
            e.Property(x => x.Address).HasColumnName("address");
        });
        b.Entity<CorporateAuthorizedSignatory>(e =>
        {
            e.ToTable("corporate_authorized_signatory");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id");

            e.Property(x => x.MemberId).HasColumnName("member_id");

            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Designation).HasColumnName("designation");
            e.Property(x => x.Pan).HasColumnName("pan");
            e.Property(x => x.Din).HasColumnName("din");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            e.Property(x => x.AadhaarLast4).HasColumnName("aadhaar_last4");
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
            e.Property(x => x.BranchId).HasColumnName("branch_id");

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
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
            e.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            e.Property(x => x.PhoneNo).HasColumnName("phone_no");
            e.Property(x => x.OldAccountNo).HasColumnName("old_account_no");
            e.Property(x => x.CustomerName).HasColumnName("customer_name");
            e.Property(x => x.InterestRate).HasColumnName("interest_rate").HasDefaultValue(0m);
            e.Property(x => x.TargetAmount).HasColumnName("target_amount").HasDefaultValue(0m);
            e.Property(x => x.PaymentDate).HasColumnName("payment_date");
            e.Property(x => x.PaidAmount).HasColumnName("paid_amount").HasDefaultValue(0m);
            e.Property(x => x.LoanAmount).HasColumnName("loan_amount").HasDefaultValue(0m);
            e.Property(x => x.BonusAmount).HasColumnName("bonus_amount").HasDefaultValue(0m);
            e.Property(x => x.InterestAmount).HasColumnName("interest_amount").HasDefaultValue(0m);
            e.Property(x => x.TotalAmount).HasColumnName("total_amount").HasDefaultValue(0m);
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.SchemeId).HasColumnName("scheme_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.FirstPaymentFlag).HasColumnName("first_payment_flag");
            e.Property(x => x.IsBidding).HasColumnName("is_bidding");
            e.Property(x => x.CustomerCode).HasColumnName("customer_code");
            e.Property(x => x.ClosedDate).HasColumnName("closed_date");


            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<AccountClosure>(e =>
        {
            e.ToTable("account_closure");
            e.HasKey(x => x.ClosureId);
            e.Property(x => x.ClosureId).HasColumnName("closure_id"); 
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.AccountNumber).HasColumnName("account_number");
            e.Property(x => x.CustomerName).HasColumnName("customer_name");
            e.Property(x => x.PhoneNo).HasColumnName("phone_no");
            e.Property(x => x.InstallmentAmount).HasColumnName("installment_amount");
            e.Property(x => x.TargetAmount).HasColumnName("target_amount").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
            e.Property(x => x.BonusAmount).HasColumnName("bonus_amount").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
            e.Property(x => x.LoanAmount).HasColumnName("loan_amount").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
            e.Property(x => x.ClosureDate).HasColumnName("closure_date");
            e.Property(x => x.Duration).HasColumnName("duration");
            e.Property(x => x.InterestAmount).HasColumnName("interest_amount").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
            e.Property(x => x.ServFeeRate).HasColumnName("serv_fee_rate").HasColumnType("decimal(10,2)");
            e.Property(x => x.ServiceFee).HasColumnName("service_fee").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
            e.Property(x => x.AmountPayable).HasColumnName("amount_payable").HasColumnType("decimal(14,2)").HasDefaultValue(0m);
            e.Property(x => x.PaymentMode).HasColumnName("payment_mode");
            e.Property(x => x.GlName).HasColumnName("gl_name");
            e.Property(x => x.GlCode).HasColumnName("gl_code");
            e.Property(x => x.VoucherNo).HasColumnName("voucher_no");
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.Durations).HasColumnName("durations");
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
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.OrgFeePct).HasColumnName("org_fee_pct");
            e.Property(x => x.SifinCommissionPct).HasColumnName("sifin_commission_pct");
            e.Property(x => x.TenantCommissionPct).HasColumnName("tenant_commission_pct");
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
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.IsApproved).HasColumnName("is_approved");
            e.Property(x => x.ApprovedBy).HasColumnName("approved_by");
            e.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            e.Property(x => x.OrgFeePct).HasColumnName("org_fee_pct");
            e.Property(x => x.SifinCommissionPct).HasColumnName("sifin_commission_pct");
            e.Property(x => x.FixedRate).HasColumnName("fixed_rate");
            e.Property(x => x.TotalBid).HasColumnName("total_bid");
            e.Property(x => x.AllotmentAmount).HasColumnName("allotment_amount");
            e.Property(x => x.TragetAmount).HasColumnName("target_amount");
            e.Property(x => x.TenantCommissionPct).HasColumnName("tenant_commission_pct");
            e.Property(x => x.BidReferenceNo).HasColumnName("bid_reference_no");


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
            e.Property(e => e.Status).HasColumnName("status").HasDefaultValue(LoanStatus.PENDING);
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            e.Property(x => x.CustomerName).HasColumnName("customer_name");
            e.Property(x => x.BidDate).HasColumnName("bid_date");
      
            //e.Property(x => x.CoopMobileNumber).HasColumnName("coop_mobile_number");
            //e.Property(x => x.CoopName).HasColumnName("coop_name");
            //e.Property(x => x.CoopAccountId).HasColumnName("coop_account_id"); 
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.AuthStatus).HasColumnName("auth_status");
            e.Property(x => x.NetDisbursementAmount).HasColumnName("net_disbursement_amount");
            e.Property(x => x.OrgFeeAmount).HasColumnName("org_fee_amount");
            e.Property(x => x.SifinCommission).HasColumnName("sifin_commission");
            e.Property(x => x.ProcessingFee).HasColumnName("processing_fee");

            e.Property(x => x.LoanRemark).HasColumnName("loan_remark");
            e.Property(x => x.LoanApplicationStatus).HasColumnName("loan_application_status");
            e.Property(x => x.SecurityDocStatus).HasColumnName("security_doc_status");
            e.Property(x => x.SecurityDocRemarks).HasColumnName("security_doc_remarks"); 
            e.Property(x => x.ChequeObtained).HasColumnName("cheque_obtained");
            e.Property(x => x.ChequeAccountNo).HasColumnName("cheque_account_no");
            e.Property(x => x.ChequeBankName).HasColumnName("cheque_bank_name");
            e.Property(x => x.ChequeNo).HasColumnName("cheque_no");
            e.Property(x => x.ChequeDate).HasColumnName("cheque_date");
            e.Property(x => x.LoanReleaseStatus).HasColumnName("loan_release_status");
            e.Property(x => x.Remarks).HasColumnName("remark");
            e.Property(x => x.DisbursementStatus).HasColumnName("disbursement_status");


            e.Property(x => x.LoanReferenceNo).HasColumnName("loan_reference_no");
            e.Property(x => x.BidReferenceNo).HasColumnName("bid_reference_no");
            e.Property(x => x.BonusAmount).HasColumnName("bonus_amount");
            e.Property(x => x.BranchCode).HasColumnName("branch_code");
            e.Property(x => x.TenantCommission).HasColumnName("tenant_commission");
            e.Property(x => x.TotalDeductions).HasColumnName("total_deductions");





            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);

        });

        b.Entity<CoBorrower>(e =>
        {
            e.ToTable("co_borrowers");
            e.HasKey(x => x.CoBorrowerId);
            e.Property(x => x.CoBorrowerId).HasColumnName("co_borrower_id").ValueGeneratedOnAdd();
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.LoanId).HasColumnName("loan_id");
            e.Property(x => x.CoBorrowerAccountId).HasColumnName("co_borrower_account_id");
            e.Property(x => x.CoBorrowerName).HasColumnName("co_borrower_name").HasMaxLength(200);
            e.Property(x => x.CoBorrowerPhone).HasColumnName("co_borrower_phone").HasMaxLength(20);
            e.Property(x => x.CoBorrowerEmail).HasColumnName("co_borrower_email").HasMaxLength(100);
            e.Property(x => x.CoBorrowerAddress).HasColumnName("co_borrower_address").HasMaxLength(500);
            e.Property(x => x.CoBorrowerAccountNumber).HasColumnName("co_borrower_account_number").HasMaxLength(50);
            e.Property(x => x.CoopName).HasColumnName("coop_name").HasMaxLength(200);
            e.Property(x => x.CoopMobileNumber).HasColumnName("coop_mobile_number").HasMaxLength(20);
            e.Property(x => x.CoopAccountId).HasColumnName("coop_account_id");
            e.Property(x => x.CoopAccountNumber).HasColumnName("coop_account_number").HasMaxLength(50);
            e.Property(x => x.CoBorrowerRemarks).HasColumnName("co_borrower_remarks").HasMaxLength(500);
            e.Property(x => x.IsPrimaryCoBorrower).HasColumnName("is_primary_co_borrower").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
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
            e.Property(x => x.Remarks).HasColumnName("remarks");
            e.Property(x => x.VoucherNo).HasColumnName("voucher_no");
            e.Property(x => x.GlAccountId).HasColumnName("gl_account_id");
            e.Property(x => x.GlAccountName).HasColumnName("gl_account_name");
            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");
            e.Property(x => x.PaymentDetailId).HasColumnName("payment_detail_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.PaymentStatus).HasColumnName("payment_status");
            e.Property(x => x.PaymentDate).HasColumnName("payment_date");
            e.Property(x => x.BranchId).HasColumnName("branch_id");

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
            e.Property(x => x.Forbank).HasColumnName("for_bank");
            e.Property(x => x.IsReported).HasColumnName("is_reported");
            e.Property(x => x.HasTransactions).HasColumnName("has_transactions");
            e.Property(x => x.HasGst).HasColumnName("has_gst");
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
            e.Property(x => x.GlId).HasColumnName("gl_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");


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
            e.Property(x => x.Tenantid).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");

        });

        b.Entity<GLAccountDailyBalance>(e =>
        {
            e.ToTable("gl_account_daily_balance");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.GlId).HasColumnName("gl_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            //e.Property(x => x.TransactionDate).HasColumnName("transaction_date");
            e.Property(x => x.Balance).HasColumnName("balance").HasColumnType("decimal(16,2)");
            //e.Property(x => x.DebitAmount).HasColumnName("debit_amount").HasColumnType("decimal(16,2)");
            //e.Property(x => x.CreditAmount).HasColumnName("credit_amount").HasColumnType("decimal(16,2)");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        });



        b.Entity<MemberAccountBalance>(e =>
        {
            e.ToTable("member_account_balance");
            e.HasKey(x => x.AccountId);
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Balance).HasColumnName("balance").HasColumnType("decimal(16,2)");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.Tenantid).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
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

        b.Entity<PaymentDetail>(e =>
        {
            e.ToTable("payment_detail");
            e.HasKey(x => x.PaymentDetailId);
            e.Property(x => x.PaymentDetailId).HasColumnName("payment_detail_id").ValueGeneratedOnAdd();
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4).IsRequired();
            e.Property(x => x.PaymentDate).HasColumnName("payment_date").IsRequired();
            e.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().IsRequired();
            e.Property(x => x.PaymentStatus).HasColumnName("payment_status").HasConversion<string>().IsRequired();
            e.Property(x => x.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(50);
            e.Property(x => x.ChequeNumber).HasColumnName("cheque_number").HasMaxLength(50);
            e.Property(x => x.BankName).HasColumnName("bank_name").HasMaxLength(100);
            e.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(50);
            e.Property(x => x.ChequeDate).HasColumnName("cheque_date");
            e.Property(x => x.UPIId).HasColumnName("upi_id").HasMaxLength(100);
            e.Property(x => x.TransactionReference).HasColumnName("transaction_reference").HasMaxLength(100);
            e.Property(x => x.Remarks).HasColumnName("remarks").HasColumnType("TEXT");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").ValueGeneratedOnAdd();
            e.Property(x => x.ModifiedBy).HasColumnName("modified_by");
            e.Property(x => x.ModifiedAt).HasColumnName("modified_at").HasDefaultValueSql("NULL ON UPDATE CURRENT_TIMESTAMP(6)").ValueGeneratedOnUpdate();
            //e.HasIndex(x => x.TenantId).HasDatabaseName("idx_payment_detail_tenant_id");
            //e.HasIndex(x => x.AccountId).HasDatabaseName("idx_payment_detail_account_id");
            //e.HasIndex(x => x.PaymentDate).HasDatabaseName("idx_payment_detail_payment_date");
            //e.HasIndex(x => x.PaymentMethod).HasDatabaseName("idx_payment_detail_payment_method");
            //e.HasIndex(x => x.PaymentStatus).HasDatabaseName("idx_payment_detail_payment_status");
            //e.HasIndex(x => x.ReferenceNumber).HasDatabaseName("idx_payment_detail_reference_number");
            //e.HasIndex(x => x.ChequeNumber).HasDatabaseName("idx_payment_detail_cheque_number");
            //e.HasIndex(x => x.UPIId).HasDatabaseName("idx_payment_detail_upi_id");
            //e.HasIndex(x => x.CreatedAt).HasDatabaseName("idx_payment_detail_created_at");

            // Optional: Composite indexes
            //e.HasIndex(x => new { x.AccountId, x.PaymentDate }).HasDatabaseName("idx_payment_detail_account_date");
            //e.HasIndex(x => new { x.TenantId, x.PaymentMethod, x.PaymentDate }).HasDatabaseName("idx_payment_detail_tenant_method_date");
        });

        b.Entity<Branch>(e =>
        {
            e.ToTable("branch");

            e.HasKey(x => x.BranchId);

            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchCode).HasColumnName("branch_code") .HasMaxLength(20);

            e.Property(x => x.BranchName).HasColumnName("branch_name")
                .HasMaxLength(100);

            e.Property(x => x.BankId).HasColumnName("bank_id");

            e.Property(x => x.RegistrationNo).HasColumnName("registration_no");

            e.Property(x => x.RegistrationDate).HasColumnName("registration_date");

            e.Property(x => x.BranchRegistrationDate).HasColumnName("branch_registration_date");

            e.Property(x => x.Address).HasColumnName("address");

            e.Property(x => x.PhoneNumber).HasColumnName("phone_number");

            //e.Property(x => x.Fax)
            //    .HasColumnName("fax");

            e.Property(x => x.Email).HasColumnName("email");

            e.Property(x => x.ReferenceNo).HasColumnName("reference_no");

            e.Property(x => x.CashGlId).HasColumnName("cash_gl_id");

            e.Property(x => x.TransferGlId).HasColumnName("transfer_gl_id");


            e.Property(x => x.AdjustmentGlId).HasColumnName("adjustment_gl_id");

            e.Property(x => x.BiddingDate).HasColumnName("bidding_date");

            e.Property(x => x.CutoffDate).HasColumnName("cutoff_date");

            e.Property(x => x.BonusPaymentDate).HasColumnName("bonus_payment_date");

            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();

            e.Property(x => x.MinimumRate).HasColumnName("minimum_rate");

            e.Property(x => x.MaximumRate).HasColumnName("maximum_rate");

            e.Property(x => x.Penalty).HasColumnName("penalty");

            e.Property(x => x.DoublePaymentAllowed).HasColumnName("double_payment_allowed");

            e.Property(x => x.MinimumInstallmentAmount).HasColumnName("minimum_installment_amount");

            e.Property(x => x.MaximumInstallmentAmount).HasColumnName("maximum_installment_amount");

            e.Property(x => x.MinimumIncrementAmount).HasColumnName("minimum_increment_amount");

            e.Property(x => x.OtherBank1) .HasColumnName("other_bank1");

            e.Property(x => x.OtherBank2).HasColumnName("other_bank2");

            e.Property(x => x.CreatedAt).HasColumnName("created_at");
              e.Property(x => x.CreatedBy).HasColumnName("created_by");

            e.Property(x => x.ModifiedAt).HasColumnName("modified_at");

            e.Property(x => x.ModifiedBy).HasColumnName("modified_by");
            e.Property(x => x.PreviousDate).HasColumnName("previous_date");
            e.Property(x => x.CurrentDate).HasColumnName("current_date");
            e.Property(x => x.NextDate).HasColumnName("next_date");

            e.HasIndex(x => new { x.TenantId, x.BranchCode })
                .IsUnique();
        });
        b.Entity<RelationMaster>(e =>
        {
            e.ToTable("relation_master");
            e.HasKey(x => x.RelationId);
            e.Property(x => x.RelationId).HasColumnName("relation_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.MemberNo).HasColumnName("member_no");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.PhoneNo).HasColumnName("phone_no").HasMaxLength(100);
            e.Property(x => x.AccountNo).HasColumnName("account_no").HasMaxLength(500);
            e.Property(x => x.Remarks).HasColumnName("remarks").HasMaxLength(500);
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdateBy).HasColumnName("updated_by");
            e.Property(x => x.UpdateAt).HasColumnName("updated_at");
            e.Property(x => x.ApprovedBy).HasColumnName("authorized_by");
            e.Property(x => x.ApprovedAt).HasColumnName("authorized_at");
            e.Property(x => x.AuthStatus)
                  .HasColumnName("status")
                  .HasConversion<string>()  // This converts enum to string
                  .HasColumnType("varchar(20)")
                  .HasMaxLength(20);
        });

        b.Entity<Bonus>(e =>
        {
            e.ToTable("bonus");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.BidReferenceNo).HasColumnName("bid_reference_no").HasMaxLength(45);
            e.Property(x => x.BounsAmount).HasColumnName("bonus_amount").HasColumnType("decimal(18,2)");
            e.Property(x => x.BidDate).HasColumnName("bid_date");
            e.Property(x => x.RecordStatus).HasColumnName("record_status").HasMaxLength(1);
            e.Property(x => x.AuthStatus).HasColumnName("auth_status").HasMaxLength(1);
            e.Property(x => x.AuthorisedBy).HasColumnName("authorised_by");
            e.Property(x => x.AuthorisedDate).HasColumnName("authorised_date");
            e.Property(x => x.BranchCode).HasColumnName("branch_code").HasMaxLength(45);
            e.Property(x => x.CoopCode).HasColumnName("coop_code").HasMaxLength(45);
            e.Property(x => x.BonusRate).HasColumnName("bonus_rate").HasColumnType("decimal(10,2)");
            e.Property(x => x.TotalMembers).HasColumnName("total_members");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.DistributedDate).HasColumnName("distributed_date");
            e.Property(x => x.DistributedStatus).HasColumnName("distributed_status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.BidReferenceNo).HasDatabaseName("idx_bid_reference_no");
            e.HasIndex(x => new { x.BranchCode, x.BranchId }).HasDatabaseName("idx_branch");
            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_tenant");
            e.HasIndex(x => x.Status).HasDatabaseName("idx_status");

            // Tenant Filter
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        // ============================================
        // BONUS DETAILS ENTITY CONFIGURATION
        // ============================================
        b.Entity<BonusDetail>(e =>
        {
            e.ToTable("bonus_details");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.BidReferenceNo).HasColumnName("bid_reference_no").HasMaxLength(45);
            e.Property(x => x.EMReferenceNo).HasColumnName("em_reference_no").HasMaxLength(45);
            e.Property(x => x.BonusAmount).HasColumnName("bonus_amount").HasColumnType("decimal(18,2)");
            e.Property(x => x.dtDate).HasColumnName("dt_date");
            e.Property(x => x.BranchCode).HasColumnName("branch_code").HasMaxLength(45);
            e.Property(x => x.CoopCode).HasColumnName("coop_code").HasMaxLength(45);
            e.Property(x => x.BonusRate).HasColumnName("bonus_rate").HasColumnType("decimal(10,2)");
            e.Property(x => x.LoanReferenceNo).HasColumnName("loan_reference_no").HasMaxLength(45);
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.MemberName).HasColumnName("member_name").HasMaxLength(255);
            e.Property(x => x.MonthlyAmount).HasColumnName("monthly_amount").HasColumnType("decimal(18,2)");
            e.Property(x => x.InterestAmount).HasColumnName("interest_amount").HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalBonusAmount).HasColumnName("total_bonus_amount").HasColumnType("decimal(18,2)");
            //e.Property(x => x.BiddingRate).HasColumnName("bidding_rate").HasColumnType("decimal(10,2)");
            //e.Property(x => x.BankCommissionRate).HasColumnName("bank_commission_rate").HasColumnType("decimal(10,2)");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // Indexes
            e.HasIndex(x => x.BidReferenceNo).HasDatabaseName("idx_bid_reference_no");
            e.HasIndex(x => x.EMReferenceNo).HasDatabaseName("idx_em_reference_no");
            e.HasIndex(x => x.LoanReferenceNo).HasDatabaseName("idx_loan_reference_no");
            e.HasIndex(x => new { x.BranchCode, x.BranchId }).HasDatabaseName("idx_branch");
            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_tenant");
            e.HasIndex(x => x.MemberId).HasDatabaseName("idx_member");
            e.HasIndex(x => x.dtDate).HasDatabaseName("idx_dt_date");
            e.HasIndex(x => x.Status).HasDatabaseName("idx_status");
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });
        b.Entity<BonusDistribution>(e =>
        {
            e.ToTable("bonus_distribution");
            e.HasKey(x => x.DistributionId);

            e.Property(x => x.DistributionId).HasColumnName("distribution_id").ValueGeneratedOnAdd();
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.BranchId).HasColumnName("branch_id");
            e.Property(x => x.BranchCode).HasColumnName("branch_code").HasMaxLength(50);
            e.Property(x => x.BidReferenceNo).HasColumnName("bid_reference_no").HasMaxLength(50).IsRequired();
            e.Property(x => x.CycleId).HasColumnName("cycle_id");
            e.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            e.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(30).IsRequired();
            e.Property(x => x.MemberId).HasColumnName("member_id").IsRequired();
            e.Property(x => x.MemberName).HasColumnName("member_name").HasMaxLength(100);
            e.Property(x => x.CustomerCode).HasColumnName("customer_code").HasMaxLength(15);
            e.Property(x => x.MonthlyContribution).HasColumnName("monthly_contribution").HasColumnType("decimal(15,2)");
            e.Property(x => x.BonusAmount).HasColumnName("bonus_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.InterestAmount).HasColumnName("interest_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.BonusRate).HasColumnName("bonus_rate").HasColumnType("decimal(10,2)");
            e.Property(x => x.InterestRate).HasColumnName("interest_rate").HasColumnType("decimal(5,2)");
            e.Property(x => x.BiddingRate).HasColumnName("bidding_rate").HasColumnType("decimal(10,2)");
            e.Property(x => x.CommissionRate).HasColumnName("commission_rate").HasColumnType("decimal(10,2)");
            e.Property(x => x.DistributionDate).HasColumnName("distribution_date");
            e.Property(x => x.BidDate).HasColumnName("bid_date");
            e.Property(x => x.PayDate).HasColumnName("pay_date");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.IsApproved).HasColumnName("is_approved");
            e.Property(x => x.ApprovedBy).HasColumnName("approved_by");
            e.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.Remarks).HasColumnName("remarks").HasMaxLength(255);
            e.Property(x => x.ReferenceNo).HasColumnName("reference_no").HasMaxLength(100);
            e.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(50);

            // Indexes
            e.HasIndex(x => new { x.TenantId, x.BranchId }).HasDatabaseName("idx_tenant_branch");
            e.HasIndex(x => x.BidReferenceNo).HasDatabaseName("idx_bid_reference");
            e.HasIndex(x => x.AccountId).HasDatabaseName("idx_account");
            e.HasIndex(x => x.AccountNumber).HasDatabaseName("idx_account_number");
            e.HasIndex(x => x.MemberId).HasDatabaseName("idx_member");
            e.HasIndex(x => x.DistributionDate).HasDatabaseName("idx_distribution_date");
            e.HasIndex(x => x.Status).HasDatabaseName("idx_status");
            e.HasIndex(x => x.CycleId).HasDatabaseName("idx_cycle");
            e.HasIndex(x => new { x.DistributionDate, x.Status }).HasDatabaseName("idx_month_year");
            e.HasIndex(x => new { x.TenantId, x.BidReferenceNo, x.Status }).HasDatabaseName("idx_tenant_bid_status");
            e.HasIndex(x => x.BonusAmount).HasDatabaseName("idx_bonus_amount");

            // Relationships
         

            // Tenant Filter (Global Query Filter)
            e.HasQueryFilter(x => _ctx.BypassTenantFilter || x.TenantId == _ctx.TenantId);
        });

        b.Entity<SifinCommission>(e =>
        {
            e.ToTable("sifin_commission");
            e.HasKey(x => x.SifinId);
            e.Property(x => x.SifinId).HasColumnName("sifin_id").ValueGeneratedOnAdd();
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.TenantName).HasColumnName("tenant_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
            e.Property(x => x.BranchCode).HasColumnName("branch_code").HasMaxLength(50);
            e.Property(x => x.LastPaidAmount).HasColumnName("last_paid_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.LastPaidDate).HasColumnName("last_paid_date");
            e.Property(x => x.CommissionAmount).HasColumnName("commission_amount").HasColumnType("decimal(15,2)").IsRequired();
            e.Property(x => x.TDSAmount).HasColumnName("tds_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.ServTaxAmount).HasColumnName("serv_tax_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.PaymentAmount).HasColumnName("payment_amount").HasColumnType("decimal(15,2)");
            e.Property(x => x.PaymentMode).HasColumnName("payment_mode").HasMaxLength(50).IsRequired().HasConversion<string>();
            e.Property(x => x.GlName).HasColumnName("gl_name").HasMaxLength(100);
            e.Property(x => x.GlCode).HasColumnName("gl_code").HasMaxLength(50);
            e.Property(x => x.VocherNo).HasColumnName("voucher_no").HasMaxLength(50).IsRequired();
            e.Property(x => x.Remark).HasColumnName("remark").HasMaxLength(100).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_tenant_id");
            e.HasIndex(x => x.TenantName).HasDatabaseName("idx_tenant_name");
            e.HasIndex(x => x.BranchId).HasDatabaseName("idx_branch_id");
            e.HasIndex(x => x.BranchCode).HasDatabaseName("idx_branch_code");
            e.HasIndex(x => x.PaymentMode).HasDatabaseName("idx_payment_mode");
            e.HasIndex(x => x.VocherNo).HasDatabaseName("idx_voucher_no");
            e.HasIndex(x => x.LastPaidDate).HasDatabaseName("idx_last_paid_date");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("idx_created_at");
            e.HasIndex(x => x.VocherNo).IsUnique().HasDatabaseName("unique_voucher_no");
        });

       b.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("password_reset_token");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.UsedAt).HasColumnName("used_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<AppUser>()
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
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
