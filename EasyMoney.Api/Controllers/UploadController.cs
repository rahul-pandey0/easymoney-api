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
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly ITenantContext _ctx;
        private readonly IMemberService _members;
        private readonly ILogger<UploadController> _log;

        public UploadController(ITenantContext ctx, IMemberService member,ILogger<UploadController> log)
        {
            _ctx = ctx;_members = member; _log = log;
        }

        [HttpPost("Filevalidate")]
        public async Task<IActionResult> FilevalidateUpload(IFormFile file, int modelId)
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
                List<string> validationErrors = null;

                var startTime = DateTime.UtcNow;
                var batchCode = $"Bic{_ctx.TenantId:00}{_ctx.BranchId:0000}-{Guid.NewGuid():N}"[..20];

                switch (modelId)
                {
                    case (int)ModelToUpload.KycDetail:
                        var kyc = await _members.InsertvalidKycData(file, fileExtension, batchCode);
                        isInsertSuccessful = kyc.isSuccess; // Await the task and access the result
                        rowCount = kyc.rowCount;
                        validationErrors = kyc.errorCsv != null
                            ? new List<string> { kyc.errorCsv } : null;
                        break;

                    default:
                        return BadRequest(new { message = "Invalid modelId provided." });
                }

                var processingTime = DateTime.UtcNow - startTime;

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
                    processingTime = processingTime.TotalSeconds,
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
        public IActionResult Upload(int modelId, string batchCode)
        {
            try
            {
                if (!Enum.IsDefined(typeof(ModelToUpload), modelId))
                    return BadRequest(new { message = "Invalid modelId provided." });

                bool isInsertSuccessful = false;
                List<string> validationErrors = null;

                var startTime = DateTime.UtcNow;

                switch (modelId)
                {
                    case (int)ModelToUpload.KycDetail:
                        var kyc = _members.InsertToDatabase(batchCode);

                        break;
                    default:
                        return BadRequest(new { message = "Invalid modelId provided." });
                }

                var processingTime = DateTime.UtcNow - startTime;

                if (!isInsertSuccessful)
                    return HandleValidationErrors(validationErrors);

                return Ok(new
                {
                    code = "Success",
                    message = "Upload successful.",
                    processingTime = processingTime.TotalSeconds,
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
                _log.LogInformation("Validation errors found. Preparing CSV download.");

                // Generate CSV content from validation errors
                var errorCsv = string.Join(Environment.NewLine, validationErrors);

                // Convert the CSV content to bytes
                var errorBytes = System.Text.Encoding.UTF8.GetBytes(errorCsv);

                // Create a custom response to include the file with a 400 status code
                var result = new FileContentResult(errorBytes, "text/csv")
                {
                    FileDownloadName = "ValidationErrors.csv"
                };

                // Set status code to 400 Bad Request
                Response.StatusCode = 400;

                return result;
            }

            _log.LogWarning("No validation errors found, returning BadRequest.");
            return BadRequest(new { message = "Validation errors occurred but no details are available." });
        }

    }
}
