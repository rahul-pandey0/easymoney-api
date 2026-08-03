using Microsoft.AspNetCore.Mvc;
using EasyMoney.Api.Services;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;

namespace EasyMoney.Api.Controllers
{
    [ApiController]
    [Route("api/v1/reports")]
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

        // Example: Account Open Report
        [HttpPost("account-report")]
        public async Task<IActionResult> GetAccountOpenReport([FromBody] ReportFilterPayload payload)
        {
            var report = await _acc.GetAccountOpenReportAsync(payload.TenantId, payload.BranchId, payload.Type, payload.FromDate, payload.ToDate);
            return Ok(report);
        }

        //// Example: KYC Report
        //[HttpGet("kyc-report")]
        //public async Task<IActionResult> GetKycReport(long tenantId, string? status)
        //{
        //    var report = await _acc.GetKycReportAsync(tenantId, status);
        //    return Ok(report);
        //}
    }
}
