using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using EasyMoney.Api.Data;
using System.Collections.Generic;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace EasyMoney.Api.Controllers
{
    [ApiController]
    [Route("api/v1/")]
    public class EODController : ControllerBase
    {
        private readonly EasyMoneyDbContext _data;
        private readonly ITenantContext _ctx;
        private readonly BiddingService _bid;
        private readonly ILogger _log;
        private readonly IBiddingService _biddingService;
        private readonly ILogger<EODController> _logger;
        //public EODController(EasyMoneyDbContext db, ITenantContext ctx, BiddingService bid, ILogger logg)
        //{
        //    _data = db; _ctx = ctx; _bid = bid; _log = logg;
        //}


        public EODController(EasyMoneyDbContext data, IBiddingService biddingService, ITenantContext ctx, ILogger<EODController> logger)
        {
            _data = data; _biddingService = biddingService; _ctx = ctx; _logger = logger;
        }

        [HttpPost("eod")]

        public async Task<IActionResult> UpdateBranchDates([FromBody] EODRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                    return BadRequest(new { Success = false, Message = "Invalid request" });

                if (request.NumberOfDays <= 0)
                    return BadRequest(new { Success = false, Message = "Number of days must be greater than 0" });

                // Get branch ID from context
                var branchId = _ctx.BranchId;

                if (branchId <= 0)
                    return BadRequest(new { Success = false, Message = "Invalid branch ID" });

                // Get branch from database
                var branch = await _data.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

                if (branch == null)
                    return NotFound(new { Success = false, Message = $"Branch not found for ID: {branchId}" });

                // Verify tenant access
                if (branch.TenantId != _ctx.TenantId)
                    return Unauthorized(new { Success = false, Message = "Unauthorized access to this branch" });

                var today = DateOnly.FromDateTime(DateTime.Today);

                // Determine base date
                DateOnly currentBaseDate;
                if (branch.CurrentDate == null || branch.CurrentDate.Value < today)
                {
                    currentBaseDate = today;
                }
                else
                {
                    currentBaseDate = branch.CurrentDate.Value;
                }

                // Calculate new dates
                var newCurrentDate = currentBaseDate.AddDays(request.NumberOfDays);
                var newPreviousDate = currentBaseDate.AddDays(request.NumberOfDays - 1);
                var newNextDate = currentBaseDate.AddDays(request.NumberOfDays + 1);

                // Update branch dates
                branch.PreviousDate = newPreviousDate;
                branch.CurrentDate = newCurrentDate;
                branch.NextDate = newNextDate;
                branch.ModifiedAt = DateTime.UtcNow;
                branch.ModifiedBy = _ctx.UserId;

                // **CYCLE OPENING LOGIC**
                BiddingCycle openedCycle = null;

                // Get the configured bidding day (e.g., 5, 10, 31)
                var configuredBiddingDay = branch.BiddingDate?.Day ?? 8;

                // Determine which cycle should be opened
                var cycleToOpen = await DetermineCycleToOpen(branch, newCurrentDate, configuredBiddingDay);

                if (cycleToOpen != null)
                {
                    try
                    {
                        openedCycle = await _biddingService.OpenCycleAsync(
                            branch.TenantId,
                            cycleToOpen.CycleMonth,
                            cycleToOpen.BiddingDate
                        );

                        _logger.LogInformation(
                            "Opened cycle {CycleId} for branch {BranchId} month {Month} with bidding date {BiddingDate} (configured: {ConfiguredDay})",
                            openedCycle.CycleId,
                            branch.BranchId,
                            cycleToOpen.CycleMonth.ToString("yyyy-MM"),
                            cycleToOpen.BiddingDate.ToString("yyyy-MM-dd"),
                            configuredBiddingDay);
                    }
                    catch (DomainException ex)
                    {
                        _logger.LogWarning(ex, "Could not open cycle for branch {BranchId} month {Month}: {Error}",
                            branch.BranchId, cycleToOpen.CycleMonth.ToString("yyyy-MM"), ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error opening cycle for branch {BranchId} month {Month}",
                            branch.BranchId, cycleToOpen.CycleMonth.ToString("yyyy-MM"));
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "No cycle opened for branch {BranchId}. CurrentDate: {CurrentDate}",
                        branch.BranchId, newCurrentDate.ToString("yyyy-MM-dd"));
                }

                // Save all changes
                await _data.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Message = $"Dates updated successfully by {request.NumberOfDays} days",
                    BranchId = branch.BranchId,
                    TenantId = branch.TenantId,
                    PreviousDate = newPreviousDate.ToString("yyyy-MM-dd"),
                    CurrentDate = newCurrentDate.ToString("yyyy-MM-dd"),
                    NextDate = newNextDate.ToString("yyyy-MM-dd"),
                    CycleOpened = openedCycle != null,
                    OpenedCycleId = openedCycle?.CycleId,
                    OpenedCycleMonth = openedCycle?.CycleMonth.ToString("yyyy-MM"),
                    OpenedBiddingDate = openedCycle?.WindowCloseAt.ToString("yyyy-MM-dd"),
                    ConfiguredBiddingDay = configuredBiddingDay
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateBranchDates");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while updating EOD dates",
                    Error = ex.Message
                });
            }
        }

        private async Task<CycleToOpen> DetermineCycleToOpen(Branch branch, DateOnly currentDate, int configuredBiddingDay)
        {
            // Get all existing cycles for this branch
            var existingCycles = await _data.BiddingCycles
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == branch.TenantId && c.BranchId == branch.BranchId)
                .OrderBy(c => c.CycleMonth)
                .ToListAsync();

            // Get the month of the current date
            var currentMonthStart = new DateOnly(currentDate.Year, currentDate.Month, 1);
            var daysInCurrentMonth = DateTime.DaysInMonth(currentDate.Year, currentDate.Month);

            // Calculate actual bidding date for current month
            var actualBiddingDay = Math.Min(configuredBiddingDay, daysInCurrentMonth);
            var biddingDateForCurrentMonth = new DateOnly(currentDate.Year, currentDate.Month, actualBiddingDay);

            // Check if current date has reached or passed the bidding date
            bool hasReachedBiddingDate = currentDate >= biddingDateForCurrentMonth;

            _logger.LogInformation(
                "Branch {BranchId}: CurrentDate={CurrentDate}, ConfiguredDay={ConfiguredDay}, " +
                "ActualBiddingDay={ActualDay}, DaysInMonth={DaysInMonth}, HasReachedBiddingDate={HasReached}, " +
                "ExistingCyclesCount={CycleCount}",
                branch.BranchId, currentDate, configuredBiddingDay, actualBiddingDay,
                daysInCurrentMonth, hasReachedBiddingDate, existingCycles.Count);

            // If we haven't reached the bidding date, don't open any cycle
            if (!hasReachedBiddingDate)
            {
                _logger.LogInformation(
                    "Branch {BranchId}: Not yet reached bidding date {BiddingDate}. Current date: {CurrentDate}",
                    branch.BranchId, biddingDateForCurrentMonth, currentDate);
                return null;
            }

            // If no cycles exist, this is a brand new branch - open for current month
            if (!existingCycles.Any())
            {
                _logger.LogInformation(
                    "Branch {BranchId}: First cycle ever - opening for month {Month}",
                    branch.BranchId, currentMonthStart.ToString("yyyy-MM"));

                return new CycleToOpen
                {
                    CycleMonth = currentMonthStart,
                    BiddingDate = biddingDateForCurrentMonth
                };
            }

            // Get the latest cycle
            var latestCycle = existingCycles.OrderByDescending(c => c.CycleMonth).First();
            var latestCycleMonth = latestCycle.CycleMonth;
            var latestCycleStatus = latestCycle.Status;

            _logger.LogInformation(
                "Branch {BranchId}: Latest cycle month: {LatestMonth}, Status: {Status}",
                branch.BranchId, latestCycleMonth.ToString("yyyy-MM"), latestCycleStatus);

            // **SCENARIO 1: Latest cycle is OPEN/PENDING (not resolved)**
            // We should NOT open any new cycle until this one is resolved
            if (latestCycleStatus != CycleStatus.RESOLVED && latestCycleStatus != CycleStatus.CLOSED &&
                latestCycleStatus != CycleStatus.NO_BID)
            {
                // Check if the current month's cycle already exists
                var currentMonthCycle = existingCycles.FirstOrDefault(c => c.CycleMonth == currentMonthStart);

                if (currentMonthCycle != null)
                {
                    _logger.LogInformation(
                        "Branch {BranchId}: Latest cycle {LatestMonth} is {Status}. " +
                        "Current month cycle {CurrentMonth} already exists with status {CurrentStatus}. " +
                        "Waiting for it to be resolved before opening next month.",
                        branch.BranchId, latestCycleMonth.ToString("yyyy-MM"), latestCycleStatus,
                        currentMonthStart.ToString("yyyy-MM"), currentMonthCycle.Status);
                }
                else
                {
                    // This is the scenario where bidding date passed but cycle wasn't created
                    // We need to create the cycle for the month that was missed
                    var monthsToCreate = new List<DateOnly>();
                    var tempMonth = latestCycleMonth.AddMonths(1);

                    while (tempMonth <= currentMonthStart)
                    {
                        var monthExists = existingCycles.Any(c => c.CycleMonth == tempMonth);
                        if (!monthExists)
                        {
                            monthsToCreate.Add(tempMonth);
                        }
                        tempMonth = tempMonth.AddMonths(1);
                    }

                    if (monthsToCreate.Any())
                    {
                        // Create the earliest missing month
                        var missingMonth = monthsToCreate.First();
                        var daysInMissingMonth = DateTime.DaysInMonth(missingMonth.Year, missingMonth.Month);
                        var actualBiddingDayForMissing = Math.Min(configuredBiddingDay, daysInMissingMonth);
                        var biddingDateForMissing = new DateOnly(missingMonth.Year, missingMonth.Month, actualBiddingDayForMissing);

                        _logger.LogInformation(
                            "Branch {BranchId}: Latest cycle {LatestMonth} is {Status}. " +
                            "Missing cycle detected for month {MissingMonth}. Creating it now.",
                            branch.BranchId, latestCycleMonth.ToString("yyyy-MM"), latestCycleStatus,
                            missingMonth.ToString("yyyy-MM"));

                        return new CycleToOpen
                        {
                            CycleMonth = missingMonth,
                            BiddingDate = biddingDateForMissing
                        };
                    }
                }

                return null;
            }

            // **SCENARIO 2: Latest cycle is RESOLVED, CLOSED, or NO_BID**
            // We can safely open the next month's cycle if we've reached its bidding date

            // Determine the next month that needs a cycle
            var nextMonthToOpen = latestCycleMonth.AddMonths(1);

            // Calculate bidding date for the next month
            var daysInNextMonth = DateTime.DaysInMonth(nextMonthToOpen.Year, nextMonthToOpen.Month);
            var actualBiddingDayForNextMonth = Math.Min(configuredBiddingDay, daysInNextMonth);
            var biddingDateForNextMonth = new DateOnly(nextMonthToOpen.Year, nextMonthToOpen.Month, actualBiddingDayForNextMonth);

            // Check if we've reached the bidding date for the next month
            bool hasReachedNextMonthBiddingDate = currentDate >= biddingDateForNextMonth;

            // Check if next month's cycle already exists
            var nextMonthCycle = existingCycles.FirstOrDefault(c => c.CycleMonth == nextMonthToOpen);

            if (hasReachedNextMonthBiddingDate)
            {
                if (nextMonthCycle == null)
                {
                    _logger.LogInformation(
                        "Branch {BranchId}: Latest cycle {LatestMonth} is {Status}. " +
                        "Reached bidding date {BiddingDate} for month {NextMonth}. " +
                        "Opening cycle for next month.",
                        branch.BranchId, latestCycleMonth.ToString("yyyy-MM"), latestCycleStatus,
                        biddingDateForNextMonth, nextMonthToOpen.ToString("yyyy-MM"));

                    return new CycleToOpen
                    {
                        CycleMonth = nextMonthToOpen,
                        BiddingDate = biddingDateForNextMonth
                    };
                }
                else if (nextMonthCycle.Status == CycleStatus.RESOLVED ||
                         nextMonthCycle.Status == CycleStatus.CLOSED ||
                         nextMonthCycle.Status == CycleStatus.NO_BID)
                {
                    // If next month is also resolved, try to open the month after
                    var monthAfterNext = nextMonthToOpen.AddMonths(1);
                    var daysInMonthAfterNext = DateTime.DaysInMonth(monthAfterNext.Year, monthAfterNext.Month);
                    var actualBiddingDayForMonthAfterNext = Math.Min(configuredBiddingDay, daysInMonthAfterNext);
                    var biddingDateForMonthAfterNext = new DateOnly(monthAfterNext.Year, monthAfterNext.Month, actualBiddingDayForMonthAfterNext);

                    if (currentDate >= biddingDateForMonthAfterNext)
                    {
                        var monthAfterNextCycle = existingCycles.FirstOrDefault(c => c.CycleMonth == monthAfterNext);

                        if (monthAfterNextCycle == null)
                        {
                            _logger.LogInformation(
                                "Branch {BranchId}: Next month's cycle {NextMonth} is also {Status}. " +
                                "Opening month after next {MonthAfterNext}.",
                                branch.BranchId, nextMonthToOpen.ToString("yyyy-MM"), nextMonthCycle.Status,
                                monthAfterNext.ToString("yyyy-MM"));

                            return new CycleToOpen
                            {
                                CycleMonth = monthAfterNext,
                                BiddingDate = biddingDateForMonthAfterNext
                            };
                        }
                    }
                }
            }

            // **SCENARIO 3: Check for any gap months that were missed**
            // This handles cases where cycles were skipped due to inactivity
            var allMonths = new List<DateOnly>();
            var firstCycleMonth = existingCycles.First().CycleMonth;
            var tempMonth2 = firstCycleMonth;

            while (tempMonth2 <= currentMonthStart)
            {
                allMonths.Add(tempMonth2);
                tempMonth2 = tempMonth2.AddMonths(1);
            }

            foreach (var month in allMonths)
            {
                var monthExists = existingCycles.Any(c => c.CycleMonth == month);

                if (!monthExists)
                {
                    // Check if we've reached the bidding date for this month
                    var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
                    var actualBiddingDayForMonth = Math.Min(configuredBiddingDay, daysInMonth);
                    var biddingDateForMonth = new DateOnly(month.Year, month.Month, actualBiddingDayForMonth);

                    if (currentDate >= biddingDateForMonth)
                    {
                        _logger.LogInformation(
                            "Branch {BranchId}: Gap detected - missing cycle for month {MissingMonth}. " +
                            "Opening it now.",
                            branch.BranchId, month.ToString("yyyy-MM"));

                        return new CycleToOpen
                        {
                            CycleMonth = month,
                            BiddingDate = biddingDateForMonth
                        };
                    }
                }
            }

            _logger.LogInformation(
                "Branch {BranchId}: No cycle needed at this time. Latest cycle: {LatestMonth} ({Status}), " +
                "Next month: {NextMonth}, Next bidding date: {NextBiddingDate}, " +
                "Next month cycle exists: {Exists}",
                branch.BranchId, latestCycleMonth.ToString("yyyy-MM"), latestCycleStatus,
                nextMonthToOpen.ToString("yyyy-MM"), biddingDateForNextMonth.ToString("yyyy-MM-dd"),
                nextMonthCycle != null);

            return null;
        }

        public class CycleToOpen
        {
            public DateOnly CycleMonth { get; set; }
            public DateOnly BiddingDate { get; set; }
        }

        //[HttpPost("update")]
        //public async Task<IActionResult> UpdateBranchDates([FromBody] EODRequest request)
        //{
        //    try
        //    {
        //        // Validate request
        //        if (request == null)
        //            return BadRequest(new { Success = false, Message = "Invalid request" });

        //        if (request.NumberOfDays <= 0)
        //            return BadRequest(new { Success = false, Message = "Number of days must be greater than 0" });

        //        // Get branch ID from context
        //        var branchId = _ctx.BranchId;

        //        if (branchId <= 0)
        //            return BadRequest(new { Success = false, Message = "Invalid branch ID" });

        //        // Get branch from database
        //        var branch = await _data.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

        //        if (branch == null)
        //            return NotFound(new { Success = false, Message = $"Branch not found for ID: {branchId}" });

        //        // Verify tenant access
        //        if (branch.TenantId != _ctx.TenantId)
        //            return Unauthorized(new { Success = false, Message = "Unauthorized access to this branch" });

        //        var today = DateOnly.FromDateTime(DateTime.Today);

        //        // Determine base date
        //        DateOnly currentBaseDate;
        //        if (branch.CurrentDate == null || branch.CurrentDate.Value < today)
        //        {
        //            currentBaseDate = today;
        //        }
        //        else
        //        {
        //            currentBaseDate = branch.CurrentDate.Value;
        //        }

        //        // Calculate new dates
        //        var newCurrentDate = currentBaseDate.AddDays(request.NumberOfDays);
        //        var newPreviousDate = currentBaseDate.AddDays(request.NumberOfDays - 1);
        //        var newNextDate = currentBaseDate.AddDays(request.NumberOfDays + 1);

        //        // Update branch dates
        //        branch.PreviousDate = newPreviousDate;
        //        branch.CurrentDate = newCurrentDate;
        //        branch.NextDate = newNextDate;
        //        branch.ModifiedAt = DateTime.UtcNow;
        //        branch.ModifiedBy = _ctx.UserId;

        //        // **UPDATED: Check if we need to open a cycle with proper month-end handling**
        //        BiddingCycle openedCycle = null;

        //        // Get the configured bidding day (e.g., 31)
        //        var configuredBiddingDay = branch.BiddingDate?.Day ?? 8;

        //        // Get the last day of the current month
        //        var lastDayOfCurrentMonth = DateTime.DaysInMonth(newCurrentDate.Year, newCurrentDate.Month);

        //        var actualBiddingDay = Math.Min(configuredBiddingDay, lastDayOfCurrentMonth);

        //        var currentDateDay = newCurrentDate.Day;
        //        var currentMonthStart = new DateOnly(newCurrentDate.Year, newCurrentDate.Month, 1);

        //        var existingCycle = await _data.BiddingCycles
        //            .IgnoreQueryFilters()
        //            .FirstOrDefaultAsync(c => c.TenantId == branch.TenantId && c.CycleMonth == currentMonthStart);

        //        bool shouldOpenCycle = false;
        //        DateOnly cycleMonthToOpen = currentMonthStart;

        //        // **UPDATED LOGIC: Handle month-end properly**
        //        // Check if we've reached or passed the bidding date for the current month
        //        bool hasReachedBiddingDate = currentDateDay >= actualBiddingDay;

        //        // Check if it's the last day of the month or beyond
        //        bool isLastDayOfMonth = currentDateDay == lastDayOfCurrentMonth;
        //        bool isPastLastDay = currentDateDay > lastDayOfCurrentMonth;

        //        _logger.LogInformation(
        //            "Branch {BranchId}: CurrentDate={CurrentDate}, Day={Day}, ConfiguredBiddingDay={ConfiguredDay}, ActualBiddingDay={ActualDay}, LastDayOfMonth={LastDay}, HasReached={HasReached}",
        //            branch.BranchId, newCurrentDate, currentDateDay, configuredBiddingDay, actualBiddingDay, lastDayOfCurrentMonth, hasReachedBiddingDate);

        //        // **CASE 1: We've reached or passed the bidding date**
        //        if (hasReachedBiddingDate)
        //        {
        //            // If no cycle exists for current month, open it
        //            if (existingCycle == null)
        //            {
        //                shouldOpenCycle = true;
        //                cycleMonthToOpen = currentMonthStart;
        //            }
        //            // If cycle is CLOSED, open next month's cycle
        //            else if (existingCycle.Status == CycleStatus.CLOSED)
        //            {
        //                shouldOpenCycle = true;
        //                cycleMonthToOpen = currentMonthStart.AddMonths(1);
        //            }
        //        }
        //        // **CASE 2: Special handling for month-end**
        //        else if (isLastDayOfMonth || isPastLastDay)
        //        {
        //            // If we're at the end of the month and bidding date hasn't been reached
        //            // (e.g., month has 30 days but bidding date is 31)
        //            // Then we should open the cycle for the next month
        //            if (existingCycle == null || existingCycle.Status == CycleStatus.CLOSED)
        //            {
        //                // Check if previous month's cycle is resolved
        //                var prevMonthStart = currentMonthStart.AddMonths(-1);
        //                var prevCycle = await _data.BiddingCycles
        //                    .IgnoreQueryFilters()
        //                    .FirstOrDefaultAsync(c => c.TenantId == branch.TenantId && c.CycleMonth == prevMonthStart);

        //                if (prevCycle == null || prevCycle.Status == CycleStatus.RESOLVED || prevCycle.Status == CycleStatus.NO_BID)
        //                {
        //                    shouldOpenCycle = true;
        //                    cycleMonthToOpen = currentMonthStart.AddMonths(1);
        //                    _logger.LogInformation("Opening next month's cycle because bidding day {BiddingDay} > last day of month {LastDay}",
        //                        configuredBiddingDay, lastDayOfCurrentMonth);
        //                }
        //            }
        //        }

        //        if (shouldOpenCycle)
        //        {
        //            try
        //            {
        //                // Calculate the correct bidding date for the cycle month
        //                var biddingDateToUse = request.BiddingDate ?? branch.BiddingDate;

        //                if (!biddingDateToUse.HasValue)
        //                {
        //                    // Calculate the actual bidding date for the cycle month
        //                    var daysInCycleMonth = DateTime.DaysInMonth(cycleMonthToOpen.Year, cycleMonthToOpen.Month);
        //                    var bidDay = Math.Min(configuredBiddingDay, daysInCycleMonth);
        //                    biddingDateToUse = new DateOnly(cycleMonthToOpen.Year, cycleMonthToOpen.Month, bidDay);
        //                }

        //                openedCycle = await _biddingService.OpenCycleAsync(
        //                    branch.TenantId,
        //                    cycleMonthToOpen,
        //                    biddingDateToUse
        //                );

        //                _logger.LogInformation(
        //                    "Opened cycle {CycleId} for branch {BranchId} month {Month} with bidding date {BiddingDate} (configured: {ConfiguredDay})",
        //                    openedCycle.CycleId,
        //                    branch.BranchId,
        //                    cycleMonthToOpen.ToString("yyyy-MM"),
        //                    biddingDateToUse.Value.ToString("yyyy-MM-dd"),
        //                    configuredBiddingDay);
        //            }
        //            catch (DomainException ex)
        //            {
        //                _logger.LogWarning(ex, "Could not open cycle for branch {BranchId}: {Error}",
        //                    branch.BranchId, ex.Message);
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, "Unexpected error opening cycle for branch {BranchId}", branch.BranchId);
        //            }
        //        }
        //        else
        //        {
        //            _logger.LogInformation(
        //                "No cycle opened for branch {BranchId}. CurrentDay: {Day}, ActualBiddingDay: {BiddingDay}, ExistingCycle: {Existing}, IsLastDay: {IsLastDay}",
        //                branch.BranchId,
        //                currentDateDay,
        //                actualBiddingDay,
        //                existingCycle?.Status.ToString() ?? "None",
        //                isLastDayOfMonth);
        //        }

        //        // Save all changes
        //        await _data.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            Success = true,
        //            Message = $"Dates updated successfully by {request.NumberOfDays} days",
        //            BranchId = branch.BranchId,
        //            TenantId = branch.TenantId,
        //            PreviousDate = newPreviousDate.ToString("yyyy-MM-dd"),
        //            CurrentDate = newCurrentDate.ToString("yyyy-MM-dd"),
        //            NextDate = newNextDate.ToString("yyyy-MM-dd"),
        //            CycleOpened = openedCycle != null,
        //            OpenedCycleId = openedCycle?.CycleId,
        //            OpenedCycleMonth = openedCycle?.CycleMonth.ToString("yyyy-MM"),
        //            OpenedBiddingDate = openedCycle?.WindowCloseAt.ToString("yyyy-MM-dd"),
        //            ConfiguredBiddingDay = configuredBiddingDay,
        //            ActualBiddingDayForMonth = actualBiddingDay,
        //            LastDayOfMonth = lastDayOfCurrentMonth
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error in UpdateBranchDates");
        //        return StatusCode(500, new
        //        {
        //            Success = false,
        //            Message = "An error occurred while updating EOD dates",
        //            Error = ex.Message
        //        });
        //    }
        //}    



        //---------------------------------------------------------------------

        //public async Task<IActionResult> UpdateBranchDates([FromBody] EODRequest request)
        //{
        //    try
        //    {

        //        if (request == null)
        //            return BadRequest(new { Success = false, Message = "Invalid request" });

        //        if (request.NumberOfDays <= 0)
        //            return BadRequest(new { Success = false, Message = "Number of days must be greater than 0" });

        //        var branchId = _ctx.BranchId;

        //        if (branchId <= 0)
        //            return BadRequest(new { Success = false, Message = "Invalid branch ID" });


        //        var branch = await _data.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

        //        if (branch == null)
        //            return NotFound(new { Success = false, Message = $"Branch not found for ID: {branchId}" });

        //        if (branch.TenantId != _ctx.TenantId)
        //            return Unauthorized(new { Success = false, Message = "Unauthorized access to this branch" });


        //        var originalPreviousDate = branch.PreviousDate;
        //        var originalCurrentDate = branch.CurrentDate;
        //        var originalNextDate = branch.NextDate;

        //        var today = DateOnly.FromDateTime(DateTime.Today);

        //        DateOnly currentBaseDate;
        //        if (branch.CurrentDate == null || branch.CurrentDate.Value < today)
        //        {
        //            currentBaseDate = today;
        //        }
        //        else
        //        {
        //            currentBaseDate = branch.CurrentDate.Value;
        //        }
        //        var newCurrentDate = currentBaseDate.AddDays(request.NumberOfDays);
        //        var newPreviousDate = currentBaseDate.AddDays(request.NumberOfDays - 1);
        //        var newNextDate = currentBaseDate.AddDays(request.NumberOfDays + 1);

        //        branch.PreviousDate = newPreviousDate;
        //        branch.CurrentDate = newCurrentDate;
        //        branch.NextDate = newNextDate;
        //        branch.ModifiedAt = DateTime.UtcNow;
        //        branch.ModifiedBy = _ctx.UserId;

        //        BiddingCycle openedCycle = null;
        //        var biddingDate = branch.BiddingDate?.Day ??0; 
        //        var currentDateDay = newCurrentDate.Day;

        //        var currentMonthStart = new DateOnly(newCurrentDate.Year, newCurrentDate.Month, 1);
        //        var existingCycle = await _data.BiddingCycles
        //            .IgnoreQueryFilters()
        //            .FirstOrDefaultAsync(c => c.TenantId == branch.TenantId && c.CycleMonth == currentMonthStart);

        //        bool shouldOpenCycle = false;
        //        DateOnly cycleMonthToOpen;

        //        if (currentDateDay >= biddingDate)
        //        {

        //            if (existingCycle == null)
        //            {
        //                shouldOpenCycle = true;
        //                cycleMonthToOpen = currentMonthStart;
        //            }
        //            else if (existingCycle.Status == CycleStatus.CLOSED)
        //            {

        //                shouldOpenCycle = true;
        //                cycleMonthToOpen = currentMonthStart.AddMonths(1);
        //            }
        //            else
        //            {

        //                shouldOpenCycle = false;
        //                cycleMonthToOpen = currentMonthStart;
        //            }
        //        }
        //        else
        //        {

        //            if (existingCycle == null && request.CycleMonth.HasValue)
        //            {
        //                shouldOpenCycle = true;
        //                cycleMonthToOpen = request.CycleMonth.Value;
        //            }
        //            else
        //            {
        //                shouldOpenCycle = false;
        //                cycleMonthToOpen = currentMonthStart;
        //            }
        //        }

        //        if (shouldOpenCycle)
        //        {
        //            try
        //            {

        //                var biddingDateToUse = request.BiddingDate ?? branch.BiddingDate;

        //                if (cycleMonthToOpen > currentMonthStart)
        //                {

        //                    if (!biddingDateToUse.HasValue)
        //                    {
        //                        var daysInMonth = DateTime.DaysInMonth(cycleMonthToOpen.Year, cycleMonthToOpen.Month);
        //                        var bidDay = Math.Min(biddingDate, daysInMonth);
        //                        biddingDateToUse = new DateOnly(cycleMonthToOpen.Year, cycleMonthToOpen.Month, bidDay);
        //                    }
        //                }

        //                openedCycle = await _bid.OpenCycleAsync(branch.TenantId, cycleMonthToOpen, biddingDateToUse);

        //                _log.LogInformation("Opened cycle {CycleId} for branch {BranchId} on EOD run",
        //                    openedCycle.CycleId, branch.BranchId);
        //            }
        //            catch (DomainException ex)
        //            {
        //                // Log but don't fail the EOD process
        //                _log.LogWarning("Could not open cycle: {Error}", ex.Message);
        //                // Continue with EOD update
        //            }
        //        }
        //        else
        //        {
        //            _log.LogInformation("No cycle opened for branch {BranchId} on EOD run. CurrentDay: {Day}, BiddingDay: {BiddingDay}",
        //                branch.BranchId, currentDateDay, biddingDate);
        //        }

        //        // Save changes
        //        await _data.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            Success = true,
        //            Message = $"Dates updated successfully by {request.NumberOfDays} days",
        //            BranchId = branch.BranchId,
        //            TenantId = branch.TenantId,
        //            PreviousDate = newPreviousDate.ToString("yyyy-MM-dd"),
        //            CurrentDate = newCurrentDate.ToString("yyyy-MM-dd"),
        //            NextDate = newNextDate.ToString("yyyy-MM-dd"),
        //            CycleOpened = openedCycle != null,
        //            OpenedCycleId = openedCycle?.CycleId,
        //            OpenedCycleMonth = openedCycle?.CycleMonth.ToString("yyyy-MM"),
        //            OpenedBiddingDate = openedCycle?.WindowCloseAt.ToString("yyyy-MM-dd")
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _log.LogError(ex, "Error in UpdateBranchDates");
        //        return StatusCode(500, new
        //        {
        //            Success = false,
        //            Message = "An error occurred while updating EOD dates",
        //            Error = ex.Message
        //        });
        //    } 
        //}
        //[HttpPost("datechange")] 
        //public async Task<IActionResult> UpdateDatesChanges([FromBody] EODRequest request)
        //{
        //    try
        //    {
        //        if (request == null)
        //            return BadRequest(new { Success = false, Message = "Invalid request" });

        //        if (request.NumberOfDays <= 0)
        //            return BadRequest(new { Success = false, Message = "Number of days must be greater than 0" });

        //        var branchId = _ctx.BranchId;

        //        if (branchId <= 0)
        //            return BadRequest(new { Success = false, Message = "Invalid branch ID" });

        //        var branch = await _data.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
        //                   if (branch == null)
        //            return NotFound(new { Success = false, Message = $"Branch not found for ID: {branchId}" });

        //        if (branch.TenantId != _ctx.TenantId)
        //            return Unauthorized(new { Success = false, Message = "Unauthorized access to this branch" });


        //        var originalPreviousDate = branch.PreviousDate;
        //        var originalCurrentDate = branch.CurrentDate;
        //        var originalNextDate = branch.NextDate;

        //        var today = DateOnly.FromDateTime(DateTime.Today);

        //        // Determine base date
        //        DateOnly currentBaseDate;
        //        if (branch.CurrentDate == null || branch.CurrentDate.Value < today)
        //        {
        //            currentBaseDate = today;
        //        }
        //        else
        //        {
        //            currentBaseDate = branch.CurrentDate.Value;
        //        }

        //        // Calculate new dates
        //        var newCurrentDate = currentBaseDate.AddDays(request.NumberOfDays);
        //        var newPreviousDate = currentBaseDate.AddDays(request.NumberOfDays - 1);
        //        var newNextDate = currentBaseDate.AddDays(request.NumberOfDays + 1);

        //        // Update branch dates
        //        branch.PreviousDate = newPreviousDate;
        //        branch.CurrentDate = newCurrentDate;
        //        branch.NextDate = newNextDate;
        //        branch.ModifiedAt = DateTime.UtcNow;
        //        branch.ModifiedBy = _ctx.UserId;

        //        // Save changes
        //        await _data.SaveChangesAsync();

        //        return Ok(new
        //        {
        //            Success = true,
        //            Message = $"Dates updated successfully by {request.NumberOfDays} days",
        //            BranchId = branch.BranchId,
        //            TenantId = branch.TenantId,
        //            PreviousDate = newPreviousDate.ToString("yyyy-MM-dd"),
        //            CurrentDate = newCurrentDate.ToString("yyyy-MM-dd"),
        //            NextDate = newNextDate.ToString("yyyy-MM-dd")
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            Success = false,
        //            Message = "An error occurred while updating EOD dates",
        //            Error = ex.Message
        //        });
        //    }
        //}


        [HttpPost("datechange")]
        public async Task<IActionResult> UpdateCurrentDate([FromBody] EODRequest? request)
        {
            try
            {
                // 1. Validate the request object exists
                if (request == null)
                    return BadRequest(new { Success = false, Message = "Invalid request" });

                // 2. Fall back to server context date ONLY if the client didn't send one
                var newCurrentDate = request.CurrentDate != default
                    ? request.CurrentDate
                    : _ctx.CurrentDate;

                if (newCurrentDate == default)
                    return BadRequest(new { Success = false, Message = "Current date is required" });

                var branchId = _ctx.BranchId;
                if (branchId <= 0)
                    return BadRequest(new { Success = false, Message = "Invalid branch ID" });

                var branch = await _data.Branches
                    .FirstOrDefaultAsync(b => b.BranchId == branchId);

                if (branch == null)
                    return NotFound(new { Success = false, Message = $"Branch not found for ID: {branchId}" });

                if (branch.TenantId != _ctx.TenantId)
                    return Unauthorized(new { Success = false, Message = "Unauthorized access to this branch" });

                // 3. Derive Previous / Next from the CHOSEN date
                var newPreviousDate = newCurrentDate.AddDays(-1);
                var newNextDate = newCurrentDate.AddDays(1);

                // 4. Apply
                branch.PreviousDate = newPreviousDate;
                branch.CurrentDate = newCurrentDate;
                branch.NextDate = newNextDate;
                branch.ModifiedAt = DateTime.UtcNow;
                branch.ModifiedBy = _ctx.UserId;

                await _data.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Message = $"Current date set to {newCurrentDate:yyyy-MM-dd}",
                    BranchId = branch.BranchId,
                    TenantId = branch.TenantId,
                    PreviousDate = newPreviousDate.ToString("yyyy-MM-dd"),
                    CurrentDate = newCurrentDate.ToString("yyyy-MM-dd"),
                    NextDate = newNextDate.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "An error occurred while updating EOD dates",
                    Error = ex.Message
                });
            }
        }
    }

        // Request model
    public class EODRequest
    {
        public int NumberOfDays { get; set; } 
        public DateOnly? CycleMonth { get; set; } // Optional: specific month to open
        public DateOnly? BiddingDate { get; set; } // Optional: specific bidding date
        public DateOnly CurrentDate { get; set; }

    }
}