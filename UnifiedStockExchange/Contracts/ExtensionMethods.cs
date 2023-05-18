using UnifiedStockExchange.Domain.Enums;

namespace UnifiedStockExchange.Contracts
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Adjust the specified <paramref name="date"/> so that it is a multiple of <paramref name="interval"/>.
        /// </summary>
        public static DateTime TruncateByInterval(this DateTime date, SampleInterval interval)
        {
            int dayMinutes = (int)date.TimeOfDay.TotalMinutes;
            date = date.Date.AddMinutes(dayMinutes / (int)interval * (int)interval);
            return date;
        }
    }
}