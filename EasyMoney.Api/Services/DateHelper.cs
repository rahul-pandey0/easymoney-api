namespace EasyMoney.Api.Services
{
    // Helpers/DateHelper.cs
    public static class DateHelper
    {
        /// <summary>
        /// Calculates the actual bidding date for a given month
        /// </summary>
        public static DateOnly GetBiddingDateForMonth(int year, int month, int configuredDay)
        {
            var lastDayOfMonth = DateTime.DaysInMonth(year, month);
            var actualDay = Math.Min(configuredDay, lastDayOfMonth);
            return new DateOnly(year, month, actualDay);
        }

        /// <summary>
        /// Checks if a date has reached or passed the bidding date
        /// </summary>
        public static bool HasReachedBiddingDate(DateOnly currentDate, int configuredBiddingDay)
        {
            var actualBiddingDate = GetBiddingDateForMonth(
                currentDate.Year,
                currentDate.Month,
                configuredBiddingDay);

            return currentDate >= actualBiddingDate;
        }

        /// <summary>
        /// Checks if a date is at the end of the month
        /// </summary>
        public static bool IsEndOfMonth(DateOnly date)
        {
            var lastDay = DateTime.DaysInMonth(date.Year, date.Month);
            return date.Day == lastDay;
        }

        /// <summary>
        /// Gets the last day of a month
        /// </summary>
        public static int GetLastDayOfMonth(int year, int month)
        {
            return DateTime.DaysInMonth(year, month);
        }
    }
}
