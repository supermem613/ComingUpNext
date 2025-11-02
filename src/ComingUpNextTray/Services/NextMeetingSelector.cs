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

            TimeSpan delta = next.StartTime - now;
            string timing;
            if (delta.TotalMinutes < 1)
            {
                timing = "Starting now";
            }
            else if (delta.TotalMinutes < 60)
            {
                timing = $"In {Math.Round(delta.TotalMinutes)} min";
            }
            else if (delta.TotalHours < 24 && next.StartTime.Date == now.Date)
            {
                timing = $"In {Math.Round(delta.TotalHours)} h";
            }
            else
            {
                timing = next.StartTime.ToString("ddd HH:mm", CultureInfo.InvariantCulture);
            }

            return $"Next: {next.Title} ({timing})";
        }
    }
}
