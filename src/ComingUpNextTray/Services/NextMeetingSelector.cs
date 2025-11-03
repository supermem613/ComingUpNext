namespace ComingUpNextTray.Services
{
    using System.Globalization;
    using ComingUpNextTray.Models;

    /// <summary>
    /// Provides helper methods to select and format information about upcoming meetings.
    /// </summary>
    internal static class NextMeetingSelector
    {
        /// <summary>
        /// Determines the next meeting starting at or after <paramref name="now"/>.
        /// </summary>
        /// <param name="entries">Collection of calendar entries.</param>
        /// <param name="now">The reference point in time.</param>
        /// <returns>The next meeting or <c>null</c> if none.</returns>
        internal static CalendarEntry? GetNextMeeting(IEnumerable<CalendarEntry> entries, DateTime now)
        {
            return entries
                .Where(e => e.StartTime >= now)
                .OrderBy(e => e.StartTime)
                .FirstOrDefault();
        }

        /// <summary>
        /// Formats a tooltip string for the specified next meeting relative to <paramref name="now"/>.
        /// </summary>
        /// <param name="next">The next meeting entry (may be null).</param>
        /// <param name="now">Current time reference.</param>
        /// <returns>User-friendly tooltip text.</returns>
        internal static string FormatTooltip(CalendarEntry? next, DateTime now)
        {
            if (next == null)
            {
                return "No upcoming meetings";
            }

            // Show absolute meeting start time instead of relative countdown.
            string absoluteTime = next.StartTime.ToString("ddd h:mm tt", CultureInfo.GetCultureInfo("en-US"));
            return $"Next: {next.Title} ({absoluteTime})";
        }
    }
}
