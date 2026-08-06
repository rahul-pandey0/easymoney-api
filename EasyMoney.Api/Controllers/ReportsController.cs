using Microsoft.AspNetCore.Mvc;
using EasyMoney.Api.Services;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;

namespace EasyMoney.Api.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class ReportsController : ControllerBase
    {
        private readonly IAccountingService _acc;
        private readonly ITenantContext _ctx;
        private readonly ILogger<ReportsController> _logger;
        private readonly IReportService _rep; 

        public ReportsController(IAccountingService acc, ITenantContext ctx, ILogger<ReportsController> logger , IReportService rep)
        {
            _acc = acc; _ctx = ctx; _logger = logger; _rep = rep;

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

        [HttpPost("payment-report")]
        public async Task<IActionResult> GetPaymentReport([FromBody] ReportFilterPayload payload)
        {
            var report = await _acc.GetPaymentReportAsync(
                payload.TenantId, payload.BranchId, payload.Type, payload.FromDate, payload.ToDate);
            return Ok(report);
        }
        [HttpPost("/reports")]
        public async Task<IActionResult> GenerateReport([FromBody] ReportFilterPayload request)
        {
            if (request == null)
                return BadRequest("Request is required.");

            var response = await _rep.GenerateReportAsync(request);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

    }
}
