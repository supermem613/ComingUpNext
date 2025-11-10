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
        /// Determines the next meeting starting at or after <paramref name="now"/>, optionally ignoring free/following entries.
        /// </summary>
        /// <param name="entries">Collection of calendar entries.</param>
        /// <param name="now">The reference point in time.</param>
        /// <param name="ignoreFreeOrFollowing">If true, entries whose title indicates free or following will be skipped.</param>
        /// <returns>The next meeting or <c>null</c> if none.</returns>
        internal static CalendarEntry? GetNextMeeting(IEnumerable<CalendarEntry> entries, DateTime now, bool ignoreFreeOrFollowing = true)
        {
            // Allow meetings that began up to 60 seconds ago to be considered 'now' so the UI holds them for an extra minute.
            DateTime lowerBound = now.AddSeconds(-60);
            IEnumerable<CalendarEntry> query = entries.Where(e => e.StartTime >= lowerBound);

            if (ignoreFreeOrFollowing)
            {
                query = query.Where(e => !IsFreeOrFollowing(e));
            }

            return query
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
            CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
            string timeFormat = next.StartTime.Date == now.Date ? "h:mm tt" : "ddd h:mm tt";
            string absoluteTime = next.StartTime.ToString(timeFormat, culture);
            return $"Next: {next.Title} ({absoluteTime})";
        }

        private static bool IsFreeOrFollowing(CalendarEntry e)
        {
            // If parsing already marked the entry, trust it.
            if (e.IsFreeOrFollowing)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(e.Title))
            {
                return false;
            }

            string t = e.Title.Trim();

            // Fallback heuristics: exact "Free" or containing the word "following".
            if (string.Equals(t, "Free", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (t.Contains("following", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
