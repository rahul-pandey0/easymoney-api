using Microsoft.AspNetCore.Mvc;
using EasyMoney.Api.Services;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;

namespace EasyMoney.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IAccountingService _acc;
        private readonly ITenantContext _ctx;

        public ReportsController(IAccountingService acc, ITenantContext ctx)
        {
            _acc = acc; _ctx = ctx;
        }

        [HttpPost("payment-report")]
        public async Task<IActionResult> GetPaymentReport([FromBody] ReportFilterPayload payload)
        {
            var report = await _acc.GetPaymentReportAsync(
                payload.TenantId, payload.BranchId, payload.Type, payload.FromDate, payload.ToDate);
            return Ok(report);
        }


        [HttpPost("kyc-report")]
        public async Task<IActionResult> GetKycReport([FromBody] ReportFilterPayload payload)
        {
            var report = await _acc.GetKycReportAsync  (payload.TenantId, payload.BranchId, payload.Type, payload.FromDate, payload.ToDate);
            return Ok(report);
        }

        //    // Example: Account Open Report
        //    [HttpGet("account-open-report")]
        //public async Task<IActionResult> GetAccountOpenReport(long tenantId, DateOnly? fromDate, DateOnly? toDate)
        //{
        //    var report = await _acc.GetAccountOpenReportAsync(tenantId, fromDate, toDate);
        //    return Ok(report);
        //}

        //// Example: KYC Report
        //[HttpGet("kyc-report")]
        //public async Task<IActionResult> GetKycReport(long tenantId, string? status)
        //{
        //    var report = await _acc.GetKycReportAsync(tenantId, status);
        //    return Ok(report);
        //}
    }
}
