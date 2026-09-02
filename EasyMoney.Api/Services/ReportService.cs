using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;

namespace EasyMoney.Api.Services
{
    public interface IReportService
    {
        Task<ReportResponse> GenerateReportAsync(ReportFilterPayload request);
    }

    public class ReportService : IReportService
    {
        private readonly IConfiguration _configuration;
        private readonly ITenantContext _ctx;
        private readonly ILogger<ReportService> _logger;

        public ReportService(IConfiguration configuration, ITenantContext ctx, ILogger<ReportService> logger)
        {
            _configuration = configuration; _ctx = ctx; _logger = logger;
        }

        public async Task<ReportResponse> GenerateReportAsync(ReportFilterPayload request)
        {
            try
            {
                if (request == null)
                    throw new ArgumentException("Request cannot be null.");

                if (!Enum.IsDefined(typeof(ReportsType), request.ReportType))
                    throw new ArgumentException($"Invalid ReportType : {request.ReportType}");

                if (request.TenantId == null && _ctx.TenantId.HasValue)
                    request.TenantId = _ctx.TenantId.Value;

                object result = ((ReportsType)request.ReportType) switch
                {
                    ReportsType.KycReports => await GetKycReportAsync(request),
                    ReportsType.CustomerAccountReport => await GetCustomerAccountReportAsync(request),
                    ReportsType.CustomerAccountsReport => await GetAccountReportAsync(request),
                    ReportsType.AccountsTranactionReports => await GetCustomerTranscationReport(request),
                    ReportsType.GetChartOfAccounts => await GetChartOfAccounts(request),
                    ReportsType.GeneralLedegerReport => await GetGlLedgerById(request),
                    ReportsType.LoanReport => await GetLoanAccountReportAsync(request),
                    ReportsType.BiddingReports => await GetBiddingReports(request),
                    ReportsType.InstallmentPayments => await GetInstallmentPaymentsReports(request),


                    _ => throw new Exception("Report type not implemented.")
                };

                return new ReportResponse
                {
                    Success = true,
                    ReportType = request.ReportType,
                    Data = result,

                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report Generation Failed");

                return new ReportResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private async Task<IEnumerable<Dictionary<string, object>>> GetKycReportAsync(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("p_GetKycReport", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_BranchId", filters.BranchId);
            command.Parameters.AddWithValue("@p_Type", filters.Type);

            command.Parameters.Add("@p_FromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }

                result.Add(row);
            }

            return result;
        }
        private async Task<IEnumerable<Dictionary<string, object>>> GetCustomerAccountReportAsync(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("p_GetAccountOpenReport", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_BranchId", filters.BranchId);
            command.Parameters.Add("@p_FromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);

                }

                result.Add(row);
            }

            return result;
        }

        private async Task<IEnumerable<Dictionary<string, object>>> GetCustomerTranscationReport(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            try
            {
                string connectionString = _configuration.GetConnectionString("Default");

                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = new MySqlCommand("p_GetPaymentReport", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 300; // Add timeout for large queries

                command.Parameters.AddWithValue("@p_TenantId", filters.TenantId > 0 ? filters.TenantId : 0);
                command.Parameters.AddWithValue("@p_BranchId", filters.BranchId > 0 ? filters.BranchId : 0);
                command.Parameters.Add("@p_FromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
                command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
                command.Parameters.Add("@p_AccountNo", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.AccountNo) ? filters.AccountNo.Trim() : DBNull.Value;

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var value = reader.GetValue(i);

                        // Handle DBNull values
                        row[columnName] = value == DBNull.Value ? null : value;
                    }

                    result.Add(row);
                }
            }
            catch (MySqlException ex)
            {
                // Handle MySQL specific errors
                Console.WriteLine($"MySQL Error: {ex.Message}");
                throw new Exception($"Database error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }

            return result;
        }
        private async Task<IEnumerable<Dictionary<string, object>>> GetChartOfAccounts(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("fn_GetChartOfAccount", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_BranchId", filters.BranchId);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }

                result.Add(row);
            }

            return result;
        }
        private async Task<IEnumerable<Dictionary<string, object>>> GetGlLedgerById(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("p_GetGlLedgerById", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_glId", filters.GlId);
            command.Parameters.Add("@p_fromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var json = reader["GlLedger"]?.ToString();

                if (!string.IsNullOrEmpty(json))
                {
                    var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (deserialized != null)
                    {
                        result.Add(deserialized);
                    }
                }
            }
            return result;
        }


        private async Task<IEnumerable<Dictionary<string, object>>> GetAccountReportAsync(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("p_GetAccountReport", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_BranchId", filters.BranchId);
            command.Parameters.Add("@p_FromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_Status", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.Status) ? filters.Status.Trim() : DBNull.Value;

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);

                }

                result.Add(row);
            }

            return result;
        }

    


       private async Task<IEnumerable<Dictionary<string, object>>> GetLoanAccountReportAsync(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("p_GetLoanReport", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_BranchId", filters.BranchId);
            command.Parameters.Add("@p_FromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_AccountNo", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.AccountNo) ? filters.AccountNo.Trim() : DBNull.Value;
            command.Parameters.Add("@p_Status", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.Status) ? filters.Status.Trim() : DBNull.Value;

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);

                }

                result.Add(row);
            }

            return result;
        }

       private async Task<IEnumerable<Dictionary<string, object>>> GetBiddingReports(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();
             
            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("p_GetBiddingReport", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_BranchId", filters.BranchId);
            command.Parameters.Add("@p_FromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_AccountNo", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.AccountNo) ? filters.AccountNo.Trim() : DBNull.Value;
            command.Parameters.Add("@p_Status", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.Status) ? filters.Status.Trim() : DBNull.Value;

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);

                }

                result.Add(row);
            }

            return result;
        }

        private async Task<IEnumerable<Dictionary<string, object>>> GetInstallmentPaymentsReports(ReportFilterPayload filters)
        {
            var result = new List<Dictionary<string, object>>();

            string connectionString = _configuration.GetConnectionString("Default");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand("p_GetInstallmentPaymentReport", connection);

            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@p_TenantId", filters.TenantId);
            command.Parameters.AddWithValue("@p_BranchId", filters.BranchId);
            command.Parameters.Add("@p_FromDate", MySqlDbType.Date).Value = filters.FromDate.HasValue ? filters.FromDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_ToDate", MySqlDbType.Date).Value = filters.ToDate.HasValue ? filters.ToDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
            command.Parameters.Add("@p_AccountNo", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.AccountNo) ? filters.AccountNo.Trim() : DBNull.Value;
            command.Parameters.Add("@p_Status", MySqlDbType.VarChar, 50).Value = !string.IsNullOrEmpty(filters.Status) ? filters.Status.Trim() : DBNull.Value;

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);

                }

                result.Add(row);
            }

            return result;
        }


    }
    

}