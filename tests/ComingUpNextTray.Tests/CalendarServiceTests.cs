using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests {
    public class CalendarServiceTests {
        [Fact]
        public void ParseIcs_ReturnsEmpty_OnEmpty() {
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs("");
            Assert.Empty(result);
        }

        [Fact]
        public void ParseIcs_ParsesSingleEvent() {
            string ics = "BEGIN:VEVENT\nSUMMARY:Test Meeting\nDTSTART:20250101T100000Z\nDTEND:20250101T103000Z\nURL:https://example.com\nEND:VEVENT";
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics);
            Models.CalendarEntry evt = Assert.Single(result);
            Assert.Equal("Test Meeting", evt.Title);
            Assert.Equal(new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc).ToLocalTime().Hour, evt.StartTime.Hour);
            // Uri normalization adds trailing slash to bare host; expect slash
            Assert.Equal("https://example.com/", evt.MeetingUrl?.ToString());
        }

        [Fact]
        public void ParseIcs_DefaultsEnd_WhenMissing() {
            string ics = "BEGIN:VEVENT\nSUMMARY:No End\nDTSTART:20250101T090000Z\nEND:VEVENT";
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics);
            Models.CalendarEntry evt = Assert.Single(result);
            Assert.True(evt.EndTime > evt.StartTime);
            Assert.Equal(1, (evt.EndTime - evt.StartTime).Hours);
        }

        [Fact]
        public void ParseIcs_FindsUrlInDescription() {
            string ics = "BEGIN:VEVENT\nSUMMARY:Desc Link\nDTSTART:20250101T090000Z\nDESCRIPTION: Join at https://example.com/meet \nEND:VEVENT";
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics);
            Models.CalendarEntry evt = Assert.Single(result);
            Assert.Equal("https://example.com/meet", evt.MeetingUrl?.ToString());
        }

        [Fact]
        public void ParseIcs_HandlesLineFolding() {
            // DESCRIPTION line folded (second line starts with space)
            string ics = "BEGIN:VEVENT\nSUMMARY:Folded Meeting\nDTSTART:20250101T100000Z\nDESCRIPTION: First part of description\n continuation with https://example.com/folded \nEND:VEVENT";
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics);
            Models.CalendarEntry evt = Assert.Single(result);
            Assert.Equal("Folded Meeting", evt.Title);
            Assert.Equal("https://example.com/folded", evt.MeetingUrl?.ToString());
        }

        [Fact]
        public void ParseIcs_AllDayEvent_DateOnly() {
            string ics = "BEGIN:VEVENT\nSUMMARY:All Day Event\nDTSTART:20250102\nEND:VEVENT"; // DTEND omitted
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics);
            Models.CalendarEntry evt = Assert.Single(result);
            Assert.Equal(new DateTime(2025, 1, 2), evt.StartTime.Date);
            Assert.Equal(1, (evt.EndTime - evt.StartTime).Hours); // default duration
        }

        [Fact]
        public void ParseIcs_SkipsMalformed_NoStart() {
            string ics = "BEGIN:VEVENT\nSUMMARY:No Start Provided\nEND:VEVENT";
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics);
            Assert.Empty(result); // should skip since no DTSTART
        }

        [Fact]
        public void ParseIcs_RRULEWithPastUntil_DoesNotGenerateFutureOccurrences()
        {
            // DTSTART in past, RRULE UNTIL set to a past date -> should not generate future occurrences
            string ics = "BEGIN:VEVENT\nSUMMARY:Past Series\nDTSTART:20230101T100000Z\nRRULE:FREQ=WEEKLY;UNTIL=20230131T235959Z\nEND:VEVENT";
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics);
            // Depending on current date this might include only the original occurrences in January 2023,
            // but must not produce any future dates. Ensure all returned starts are <= UNTIL.
            DateTime until = DateTime.SpecifyKind(new DateTime(2023, 1, 31, 23, 59, 59), DateTimeKind.Utc).ToLocalTime();
            Assert.All(result, e => Assert.True(e.StartTime <= until, "Found a generated occurrence after UNTIL"));
        }

        [Fact]
        public void ParseIcs_BiweeklyByDay_FindsMeetingOnUntilBoundary()
        {
            // VEVENT copied from user's calendar: biweekly Tuesday 09:05 PT, UNTIL 2026-11-03T17:05Z
            string ics = "BEGIN:VEVENT\nRRULE:FREQ=WEEKLY;UNTIL=20261103T170500Z;INTERVAL=2;BYDAY=TU;WKST=SU\nEXDATE;TZID=Pacific Standard Time:20250729T090500,20250923T090500\nUID:uid\nSUMMARY:1:1 Marcus, Bhavesh\nDTSTART;TZID=Pacific Standard Time:20250422T090500\nDTEND;TZID=Pacific Standard Time:20250422T093000\nSTATUS:CONFIRMED\nEND:VEVENT";

            // Use a deterministic 'now' on 2025-11-04 to see if an occurrence exists on that date
            DateTime now = new DateTime(2025, 11, 4, 0, 0, 0, DateTimeKind.Local);
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics, now);

            // There should be at least one entry whose StartTime.Date equals 2025-11-04 in local time
            Assert.Contains(result, e => e.StartTime.Date == now.Date && e.Title.Contains("1:1"));
        }
    }
}
