namespace ComingUpNextTray.Services
{
    using System;
    using System.Collections.Generic;
    using ComingUpNextTray.Models;

    /// <summary>
    /// Internal facade to provide convenience helpers for tooling.
    /// </summary>
    internal static class CalendarFacade
    {
        /// <summary>
        /// Determine the next meeting from ICS content using the application's selection rules.
        /// </summary>
        /// <param name="ics">ICS content.</param>
        /// <param name="ignoreFreeOrFollowing">Whether free/following entries should be ignored.</param>
        /// <param name="now">Optional 'now' reference.</param>
        /// <returns>DTO for the next meeting or null if none.</returns>
        internal static NextMeetingDto? GetNextMeeting(string ics, bool ignoreFreeOrFollowing = true, DateTime? now = null)
        {
            DateTime nowLocal = now ?? DateTime.Now;
            var entries = CalendarService.ParseIcs(ics, nowLocal);

            // Use internal selector to pick the next one
            var next = NextMeetingSelector.GetNextMeeting(entries, nowLocal, ignoreFreeOrFollowing);

            if (next is null)
            {
                return null;
            }

            return new NextMeetingDto
            {
                Title = next.Title,
                StartTime = next.StartTime,
                EndTime = next.EndTime,
                MeetingUrl = next.MeetingUrl?.ToString(),
                IsFreeOrFollowing = next.IsFreeOrFollowing,
            };
        }
    }
}
