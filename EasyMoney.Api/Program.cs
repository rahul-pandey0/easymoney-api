using System.Text;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Jobs;
using EasyMoney.Api.Middleware;
using EasyMoney.Api.Services;
using EasyMoney.Api.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.MySql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

// ============================================================
// Bootstrap Serilog early so startup errors are captured
// ============================================================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {TenantId:0000} {UserId:0000} {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/easymoney-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {TenantId} {UserId} {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ============================================================
    // Serilog request logging
    // ============================================================
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] tid={TenantId} uid={UserId} {CorrelationId} {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/easymoney-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] tid={TenantId} uid={UserId} rid={CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // ==== Tenant context (scoped, populated by TenantContextMiddleware) ====
    builder.Services.AddScoped<ITenantContext, TenantContext>();

    // ==== DbContext ====
    var connStr = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default missing");
    builder.Services.AddDbContext<EasyMoneyDbContext>(opt =>
        opt.UseMySql(connStr, ServerVersion.AutoDetect(connStr),
            my => my.EnableRetryOnFailure()));

    // ==== JWT ====
    var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
        ?? throw new InvalidOperationException("Jwt config missing");
    builder.Services.AddSingleton(jwt);
    builder.Services.AddScoped<ITokenService, TokenService>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
    builder.Services.AddAuthorization();

    // ==== Hangfire ====
    var hangfireConnStr = builder.Configuration.GetConnectionString("Hangfire") ?? connStr;
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseStorage(new MySqlStorage(hangfireConnStr, new MySqlStorageOptions
        {
            TablesPrefix = "hangfire_",
            QueuePollInterval = TimeSpan.FromSeconds(15),
            JobExpirationCheckInterval = TimeSpan.FromHours(1),
            CountersAggregateInterval = TimeSpan.FromMinutes(5),
            PrepareSchemaIfNecessary = true
        })));
    builder.Services.AddHangfireServer(opt =>
    {
        opt.WorkerCount = 2;
        opt.Queues = new[] { "default" };
    });
    builder.Services.AddScoped<MonthlyCycleJob>();
    builder.Services.AddScoped<DueReminderJob>();

    // ==== Business services ====
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IAccountingService, AccountingService>();
    builder.Services.AddScoped<IKycService, KycService>();
    builder.Services.AddScoped<IKycDocumentService, KycDocumentService>();
    builder.Services.AddScoped<IMemberService, MemberService>();
    builder.Services.AddScoped<ITenantService, TenantService>();
    builder.Services.AddScoped<IApprovalService, ApprovalService>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<ILedgerService, LedgerService>();
    builder.Services.AddScoped<ILoanService, LoanService>();
    builder.Services.AddScoped<IBiddingService, BiddingService>();
    builder.Services.AddScoped<IExitService, ExitService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<DashboardService>();
    // ==== CORS ====
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(opt => opt.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                  .SetIsOriginAllowedToAllowWildcardSubdomains();
        else
            policy.AllowAnyOrigin();   // dev fallback when no origins configured
        policy.AllowAnyHeader().AllowAnyMethod();
    }));

    // ==== FluentValidation ====
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

    // ==== Controllers + Swagger (with Bearer support) ====
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "EasyMoney API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT bearer token. Paste the access token from /api/v1/auth/login (without the 'Bearer ' prefix)."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // ==== Serilog request enricher: stamps TenantId + UserId + CorrelationId onto every log ====
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, httpCtx) =>
        {
            var tenantId = httpCtx.User.FindFirst("tenant_id")?.Value ?? "-";
            var userId = httpCtx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "-";
            var correlationId = httpCtx.TraceIdentifier;
            diag.Set("TenantId", tenantId);
            diag.Set("UserId", userId);
            diag.Set("CorrelationId", correlationId);
        };
    });

    // ==== Pipeline ====
   // if (app.Environment.IsDevelopment())
    //{
        app.UseSwagger();
        app.UseSwaggerUI();
    //}

    app.UseCors();
    app.UseAuthentication();
    app.UseMiddleware<TenantContextMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();
    app.UseAuthorization();

    // ==== Hangfire dashboard (SIFIN/admin only — simple IP restriction in dev) ====
    if (app.Environment.IsDevelopment())
    {
        app.UseHangfireDashboard("/hangfire");
    }

    app.MapGet("/health", () => Results.Ok(new { status = "ok", phase = 7 }));
    app.MapGet("/health/db", async (EasyMoneyDbContext db) =>
    {
        var canConnect = await db.Database.CanConnectAsync();
        var tenants = canConnect ? await db.Tenants.IgnoreQueryFilters().CountAsync() : -1;
        var users = canConnect ? await db.AppUsers.IgnoreQueryFilters().CountAsync() : -1;
        return Results.Ok(new { canConnect, tenants, users });
    });
    app.MapControllers();

    // ==== Bootstrap: seed SIFIN_ADMIN + SIFIN_AUTHORIZER if no users exist ====
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EasyMoneyDbContext>();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        if (tenantCtx is TenantContext tc) tc.SetBypass(true);

        if (!await db.AppUsers.IgnoreQueryFilters().AnyAsync())
        {
            var adminEmail = app.Configuration["Seed:SifinAdminEmail"] ?? "admin@easymoney.local";
            var adminPwd   = app.Configuration["Seed:SifinAdminPassword"] ?? "ChangeMe!2026";
            var chkEmail   = app.Configuration["Seed:SifinAuthorizerEmail"] ?? "checker@easymoney.local";
            var chkPwd     = app.Configuration["Seed:SifinAuthorizerPassword"] ?? "ChangeMe!2026";

            var admin = new AppUser
            {
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPwd),
                Role = UserRole.SIFIN_ADMIN,
                IsActive = true,
                AuthorizedAt = DateTime.UtcNow
            };
            db.AppUsers.Add(admin);
            await db.SaveChangesAsync();
            admin.AuthorizedBy = admin.UserId;
            admin.CreatedBy = admin.UserId;

            db.AppUsers.Add(new AppUser
            {
                Email = chkEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(chkPwd),
                Role = UserRole.SIFIN_AUTHORIZER,
                IsActive = true,
                CreatedBy = admin.UserId,
                AuthorizedBy = admin.UserId,
                AuthorizedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            app.Logger.LogInformation("Seeded SIFIN_ADMIN={Admin} and SIFIN_AUTHORIZER={Checker}",
                adminEmail, chkEmail);
        }
    }

    // ==== Hangfire recurring job: open cycle + generate dues on the 1st of each month ====
    //RecurringJob.AddOrUpdate<MonthlyCycleJob>(
    //    "monthly-cycle",
    //    job => job.ExecuteAsync(),
    //    Cron.Monthly(1, 0)); // 1st of month, midnight UTC

    //RecurringJob.AddOrUpdate<DueReminderJob>(
    //    "due-reminder-daily",
    //    job => job.ExecuteAsync(),
    //    Cron.Daily(8)); // every day at 08:00 UTC

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
