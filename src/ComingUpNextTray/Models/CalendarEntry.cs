namespace ComingUpNextTray.Models
{
    /// <summary>
    /// Represents a single calendar entry (VEVENT) parsed from an ICS file.
    /// </summary>
    internal sealed class CalendarEntry
    {
        /// <summary>
        /// Gets or sets the human-readable title (SUMMARY) of the event.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start time of the event in local time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the end time of the event in local time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the meeting URL discovered in URL or DESCRIPTION fields.
        /// </summary>
        public Uri? MeetingUrl { get; set; }

        /// <inheritdoc />
        public override string ToString() => $"{this.Title} @ {this.StartTime:u}";
    }
}
