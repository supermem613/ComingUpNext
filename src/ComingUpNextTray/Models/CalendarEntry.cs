namespace ComingUpNextTray.Models {
    public sealed class CalendarEntry {
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? MeetingUrl { get; set; }

        public override string ToString() => $"{Title} @ {StartTime:u}";
    }
}
