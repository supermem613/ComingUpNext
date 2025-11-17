namespace ComingUpNextTray.Services
{
    using System;

    /// <summary>
    /// Internal DTO representing the selected next meeting for tooling.
    /// </summary>
    internal sealed class NextMeetingDto
    {
        /// <summary>
        /// Gets or sets the human readable meeting title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the local start time for the meeting.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the local end time for the meeting.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the meeting join URL, if known.
        /// </summary>
        public string? MeetingUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the parser determined this is a free/placeholder entry.
        /// </summary>
        public bool IsFreeOrFollowing { get; set; }
    }
}
