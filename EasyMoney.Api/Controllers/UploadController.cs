using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Globalization;

namespace EasyMoney.Api.Controllers
{
    [ApiController]
    [Route("api/v1/upload")]
    //[Authorize]
    public class UploadController : ControllerBase
    {
        private readonly ITenantContext _ctx;
        private readonly IMemberService _members;
        private readonly ILogger<UploadController> _log;
        private readonly IAccountService _acc; 
        private readonly IBiddingService _bid; 
        private readonly ILoanService _loan;


        public UploadController(ITenantContext ctx, IMemberService member,ILogger<UploadController> log, IAccountService acc,IBiddingService bid ,ILoanService loan)
        {
            _ctx = ctx;_members = member; _log = log; _acc = acc;_bid = bid; _loan = loan;
        }

        [HttpPost("Filevalidate")]
        //Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.Member)]

        public async Task<IActionResult> FilevalidateUpload(IFormFile file, int modelId)
        //public async Task<IActionResult> FilevalidateUpload([FromForm] IFormFile file, [FromQuery] int modelId)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "File is missing or empty." });

                var fileExtension = Path.GetExtension(file.FileName)?.ToLower();
                if (fileExtension != ".csv" && fileExtension != ".xlsx")
                    return BadRequest(new { message = "Invalid file format. Please upload a CSV or Excel file." });

                if (!Enum.IsDefined(typeof(ModelToUpload), modelId))
                    return BadRequest(new { message = "Invalid modelId provided." });

                int rowCount = 0;
                bool isInsertSuccessful = false;
                List<string>? validationErrors = null;

                //var startTime = DateTime.UtcNow;
                var batchCode = $"Bic{_ctx.TenantId:00}{_ctx.BranchId:0000}-{Guid.NewGuid():N}"[..20];

                switch (modelId)
                {
                    case (int)ModelToUpload.KycDetail:
                        var kyc = await _members.InsertvalidKycData(file, fileExtension, batchCode);
                        isInsertSuccessful = kyc.isSuccess; 
                        rowCount = kyc.rowCount;
                        validationErrors = kyc.errorCsv != null ? new List<string> { kyc.errorCsv } : null;
                        break;
                         
                    case (int)ModelToUpload.AccountDetails:
                        var account = await _acc.InsertvalidAccData(file, fileExtension, batchCode);
                        isInsertSuccessful = account.isSuccess; 
                        rowCount = account.rowCount;
                        validationErrors = account.errorCsv != null ? new List<string> { account.errorCsv } : null;
                        break;

                    case (int)ModelToUpload.LedgerDetails:
                        var ledger = await _acc.InsertvalidLedgerData(file, fileExtension, batchCode);
                        isInsertSuccessful = ledger.isSuccess; 
                        rowCount = ledger.rowCount;
                        validationErrors = ledger.errorCsv != null ? new List<string> { ledger.errorCsv } : null;
                        break;

                    case (int)ModelToUpload.PaymentData:
                        var payment = await _acc.InsertvalidApaymentData(file, fileExtension, batchCode);
                        isInsertSuccessful = payment.isSuccess; 
                        rowCount = payment.rowCount;
                        validationErrors = payment.errorCsv != null ? new List<string> { payment.errorCsv } : null;
                        break;


                    case (int)ModelToUpload.Bid:
                        var Bid = await _bid.InsertvalidData(file, fileExtension, batchCode);
                        isInsertSuccessful = Bid.isSuccess;
                        rowCount = Bid.rowCount;
                        validationErrors = Bid.errorCsv != null ? new List<string> { Bid.errorCsv } : null;
                        break;

                    case (int)ModelToUpload.Loan:
                        var loan = await _loan.InsertvalidData(file, fileExtension, batchCode);
                        isInsertSuccessful = loan.isSuccess;
                        rowCount = loan.rowCount;
                        validationErrors = loan.errorCsv != null ? new List<string> { loan.errorCsv } : null;
                        break;

                    default:
                        return BadRequest(new { message = "Invalid modelId provided." });
                }

                var processingTime = _ctx.TenantId + "" + _ctx.BranchId + "" + _ctx.CurrentDate.ToDateTime(TimeOnly.MinValue);

                if (!isInsertSuccessful)
                {
                    if (validationErrors != null && validationErrors[0].Contains("failed: Download CSV."))
                        return BadRequest(new { message = "failed: Download CSV." });
                    return HandleValidationErrors(validationErrors);
                }

                return Ok(new
                {
                    code = "Success",
                    message = "Validation successful.",
                    rowCount,
                    processingTime = processingTime,
                    batchCode
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error during KYC file upload for ModelId {ModelId}.", modelId);
                return StatusCode(500, new { message = "An error occurred." });
            }
        }

        [HttpPost("validationadata")]
        //Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.Member)]

        public IActionResult Upload(int modelId, string batchCode)
        {
            try
            {
                if (!Enum.IsDefined(typeof(ModelToUpload), modelId))
                    return BadRequest(new { message = "Invalid modelId provided." });

                bool isInsertSuccessful = false;
                List<string> validationErrors = null;

                //var startTime = DateTime.UtcNow;

                switch (modelId)
                {
                    case (int)ModelToUpload.KycDetail:
                        var kyc = _members.InsertToDatabase(batchCode);
                        isInsertSuccessful = kyc.Result.isSuccess;
                        break;


                    case (int)ModelToUpload.AccountDetails:
                        var acc = _acc.InsertToDatabase(batchCode);
                        isInsertSuccessful = acc.Result.isSuccess;
                        break;

                    case (int)ModelToUpload.PaymentData:
                        var payment = _acc.InsertToPaymentDatabase(batchCode);
                        isInsertSuccessful = payment.Result.isSuccess;
                        break;


                    case (int)ModelToUpload.LedgerDetails:
                        var ledger = _acc.InsertToLedgerDatabase(batchCode);
                        isInsertSuccessful = ledger.Result.isSuccess;
                        break;


                    case (int)ModelToUpload.Bid:
                        var bid = _bid.InsertToDatabase(batchCode);
                        isInsertSuccessful = bid.Result.isSuccess;
                        break;


                    case (int)ModelToUpload.Loan:
                        var loan = _loan.InsertToDatabase(batchCode);
                        isInsertSuccessful = loan.Result.isSuccess;
                        break;


                    default:
                        return BadRequest(new { message = "Invalid modelId provided." });
                }

                var processingTime =_ctx.TenantId+""+_ctx.BranchId + "" + _ctx.CurrentDate.ToDateTime(TimeOnly.MinValue);

                if (!isInsertSuccessful)
                {
                    if (validationErrors != null && validationErrors[0].Contains("failed: Download CSV."))
                        return BadRequest(new { message = "failed: Download CSV." });
                    return HandleValidationErrors(validationErrors);
                }
                return Ok(new
                {
                    code = "Success",
                    message = "Upload successful.",
                    processingTime = processingTime,
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error during KYC processing for ModelId {ModelId}.", modelId);
                return StatusCode(500, new { message = "An error occurred." });
            }
        }

        private IActionResult HandleValidationErrors(List<string> validationErrors)
        {
            if (validationErrors != null && validationErrors.Any())
            {
                _log.LogInformation("Validation errors found.");
                return BadRequest(new
                {
                    message = "Validation failed.",
                    errors = validationErrors
                });

            }
            return null;
        }

        //private IActionResult HandleValidationErrors(List<string> validationErrors)
        //{
        //    if (validationErrors != null && validationErrors.Any())
        //    {
        //        _log.LogInformation("Validation errors found. Preparing CSV download.");

        //        // Generate CSV content from validation errors
        //        var errorCsv = string.Join(Environment.NewLine, validationErrors);

        //        // Convert the CSV content to bytes
        //        var errorBytes = System.Text.Encoding.UTF8.GetBytes(errorCsv);

        //        // Create a custom response to include the file with a 400 status code
        //        var result = new FileContentResult(errorBytes, "text/csv")
        //        {
        //            FileDownloadName = "ValidationErrors.csv"
        //        };

        //        // Set status code to 400 Bad Request
        //        Response.StatusCode = 400;

        //        return result;
        //    }

        //    _log.LogWarning("No validation errors found, returning BadRequest.");
        //    return BadRequest(new { message = "Validation errors occurred but no details are available." });
        //}

    }
}
