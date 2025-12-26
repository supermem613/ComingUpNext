namespace ComingUpNextTray.Models
{
    /// <summary>
    /// Represents a single calendar entry (VEVENT) parsed from an ICS file.
    /// </summary>
    internal sealed class CalendarEntry
    {
        /// <summary>
        /// Gets or sets the event UID (UID) when present. Used to correlate recurrence exceptions/cancellations.
        /// </summary>
        public string? Uid { get; set; }

        /// <summary>
        /// Gets or sets the recurrence instance identifier (RECURRENCE-ID) when present.
        /// For Outlook/Exchange cancellations this often denotes the cancelled instance start.
        /// </summary>
        public DateTime? RecurrenceId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the VEVENT represents a cancelled event/instance (STATUS:CANCELLED).
        /// Cancelled entries should not be shown as upcoming meetings.
        /// </summary>
        public bool IsCancelled { get; set; }

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

        /// <summary>
        /// Gets or sets a value indicating whether this entry should be considered free/placeholder (e.g. TRANSP:TRANSPARENT, STATUS:FREE or summary markers).
        /// </summary>
        public bool IsFreeOrFollowing { get; set; }

        /// <inheritdoc />
        public override string ToString() => $"{this.Title} @ {this.StartTime:u}";
    }
}
