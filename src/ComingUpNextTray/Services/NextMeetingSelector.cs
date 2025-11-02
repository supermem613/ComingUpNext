using ComingUpNextTray.Models;

namespace ComingUpNextTray.Services {
    public static class NextMeetingSelector {
        public static CalendarEntry? GetNextMeeting(IEnumerable<CalendarEntry> entries, DateTime now) {
            return entries
                .Where(e => e.StartTime >= now)
                .OrderBy(e => e.StartTime)
                .FirstOrDefault();
        }

        public static string FormatTooltip(CalendarEntry? next, DateTime now) {
            if (next == null) {
                return "No upcoming meetings";
            }

            TimeSpan delta = next.StartTime - now;
            string timing;
            if (delta.TotalMinutes < 1) {
                timing = "Starting now";
            }
            else if (delta.TotalMinutes < 60) {
                timing = $"In {Math.Round(delta.TotalMinutes)} min";
            }
            else if (delta.TotalHours < 24 && next.StartTime.Date == now.Date) {
                timing = $"In {Math.Round(delta.TotalHours)} h";
            }
            else {
                timing = next.StartTime.ToString("ddd HH:mm");
            }

            return $"Next: {next.Title} ({timing})";
        }
    }
}
