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
            // Verify time in UTC to avoid time zone differences
            Assert.Equal(10, evt.StartTime.ToUniversalTime().Hour);
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

            // Use a deterministic 'now' on 2025-11-04 12:00 UTC (safe time that's Nov 4 in all timezones)
            DateTime now = new DateTime(2025, 11, 4, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics, now);

            // There should be at least one entry whose StartTime.Date equals 2025-11-04 (checking in local time)
            DateTime expectedDate = new DateTime(2025, 11, 4);
            Assert.Contains(result, e => e.StartTime.Date == expectedDate && e.Title.Contains("1:1"));
        }

        [Fact]
        public void ParseIcs_MarksExchangeFreeAndPreservesOnExpansion()
        {
            // sanitized VEVENT similar to user's "ODSP Design Review" with vendor busy-status
            string ics =
                "BEGIN:VEVENT\n" +
                "UID:sample-uid\n" +
                "SUMMARY:Sanitized Free Meeting\n" +
                "DTSTART;TZID=Pacific Standard Time:20250825T100000\n" +
                "DTEND;TZID=Pacific Standard Time:20250825T110000\n" +
                "TRANSP:TRANSPARENT\n" +
                "STATUS:CONFIRMED\n" +
                "X-MICROSOFT-CDO-BUSYSTATUS:FREE\n" +
                "RRULE:FREQ=WEEKLY;COUNT=3\n" +
                "END:VEVENT";

            // Use a 'now' before the DTSTART to ensure expansion returns occurrences
            DateTime now = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics, now);

            // There should be multiple occurrences (original + expansions)
            Assert.True(result.Count >= 1, "Expected at least one occurrence");

            // All occurrences should be marked as free/placeholder
            Assert.All(result, e => Assert.True(e.IsFreeOrFollowing, $"Entry {e} was not marked free"));
        }

        [Fact]
        public void ParseIcs_RRULEWithUntilOnTuesday_GeneratesTuesdayWhenWeekStartsLater()
        {
            // Regression test for bug where RRULE BYDAY=TU,TH with UNTIL on a Tuesday
            // would skip the final Tuesday occurrence when the week's generation start
            // point fell on Thursday (after Tuesday).
            //
            // DTSTART: Thursday Dec 4, 2025 @ 10:00 UTC (no DST issues in December)
            // RRULE: Weekly on Tuesday and Thursday
            // UNTIL: Tuesday Dec 30, 2025 @ 23:59 UTC
            //
            // The bug: expansion loop would start a week on a Thursday, see that
            // the next Thursday > UNTIL, and exit without generating the prior Tuesday.
            string ics = "BEGIN:VEVENT\nUID:fab-eng-sync\nSUMMARY:FAB Eng Sync\nDTSTART:20251204T100000Z\nDTEND:20251204T110000Z\nRRULE:FREQ=WEEKLY;UNTIL=20251230T235900Z;INTERVAL=1;BYDAY=TU,TH;WKST=SU\nSTATUS:CONFIRMED\nEND:VEVENT";

            // Query on Dec 30, 2025 at 10:00 UTC (before the meetings on that day)
            DateTime now = new DateTime(2025, 12, 30, 10, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics, now);

            // The recurrence should generate meetings on Tuesdays and Thursdays up to Dec 30
            // Find the Tuesday Dec 30 occurrence (test is verifying it's generated)
            DateTime dec30 = new DateTime(2025, 12, 30);
            Models.CalendarEntry? tuesdayOccurrence = result.FirstOrDefault(e =>
                e.StartTime.Date == dec30 &&
                e.Title == "FAB Eng Sync");

            Assert.NotNull(tuesdayOccurrence);
            // Verify it's at the correct time (10:00 UTC)
            DateTime startUtc = tuesdayOccurrence.StartTime.ToUniversalTime();
            Assert.Equal(10, startUtc.Hour);
            Assert.Equal(0, startUtc.Minute);
        }

        [Fact]
        public void ParseIcs_RRULEWithUntilOnMonday_DoesNotGenerateLaterDaysInWeek()
        {
            // Test that UNTIL date is honored - days AFTER the UNTIL should not be generated
            // even if they're in the same calendar week
            string ics =
                "BEGIN:VEVENT\n" +
                "UID:multi-day-test\n" +
                "SUMMARY:Multi Day Meeting\n" +
                "DTSTART:20250901T100000Z\n" +
                "DTEND:20250901T110000Z\n" +
                "RRULE:FREQ=WEEKLY;UNTIL=20251124T100000Z;BYDAY=TU,WE,TH\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT";

            // Query on Monday Nov 24, 2025 at 9 AM - before the UNTIL time (10 AM)
            DateTime now = new DateTime(2025, 11, 24, 9, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics, now);

            // The UNTIL is Monday Nov 24 at 10 AM UTC
            // So Tuesday Nov 25, Wed Nov 26, Thu Nov 27 should NOT be generated (they're after UNTIL)
            var nov25 = result.FirstOrDefault(e => e.StartTime.Date == new DateTime(2025, 11, 25));
            var nov26 = result.FirstOrDefault(e => e.StartTime.Date == new DateTime(2025, 11, 26));
            var nov27 = result.FirstOrDefault(e => e.StartTime.Date == new DateTime(2025, 11, 27));

            // None should exist - they're all after the UNTIL datetime
            Assert.Null(nov25);
            Assert.Null(nov26);
            Assert.Null(nov27);

            // But we should have earlier occurrences (e.g., Nov 18, 19, 20)
            Assert.NotEmpty(result);
        }

        [Fact]
        public void ParseIcs_RRULEWithUntilIncludesOccurrencesUpToUntilTime()
        {
            // Verify that occurrences ON the UNTIL date but before UNTIL time are included
            string ics =
                "BEGIN:VEVENT\n" +
                "UID:date-time-until-test\n" +
                "SUMMARY:Daily Standup\n" +
                "DTSTART:20251201T090000Z\n" +
                "DTEND:20251201T091500Z\n" +
                "RRULE:FREQ=WEEKLY;UNTIL=20251212T120000Z;BYDAY=MO,TU,WE,TH,FR\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT";

            // UNTIL is Dec 12 at 12:00 UTC (noon), meeting is at 9:00 AM UTC daily
            // So Dec 12 should be included since 9 AM < noon
            DateTime now = new DateTime(2025, 12, 11, 8, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> result = CalendarService.ParseIcs(ics, now);

            // Should include Dec 12 (Friday) since meeting time (9 AM) is before UNTIL time (noon)
            var dec12 = result.FirstOrDefault(e => e.StartTime.Date == new DateTime(2025, 12, 12));
            Assert.NotNull(dec12);
        }

        [Fact]
        public void ParseIcs_CancelledRecurrenceId_ExcludesCancelledOccurrence()
        {
            // Outlook/Exchange commonly emits cancelled single occurrences as a separate VEVENT:
            // UID + RECURRENCE-ID + STATUS:CANCELLED.
            // Ensure we do not surface that instance as the next meeting.
            string ics =
                "BEGIN:VCALENDAR\n" +
                "BEGIN:VEVENT\n" +
                "UID:exercise-pinny\n" +
                "SUMMARY:Exercise @ Pinny\n" +
                "DTSTART:20251219T180000Z\n" +
                "DTEND:20251219T190000Z\n" +
                "RRULE:FREQ=WEEKLY;BYDAY=FR;UNTIL=20260131T235900Z\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT\n" +
                "BEGIN:VEVENT\n" +
                "UID:exercise-pinny\n" +
                "SUMMARY:Exercise @ Pinny\n" +
                "RECURRENCE-ID:20251226T180000Z\n" +
                "DTSTART:20251226T180000Z\n" +
                "DTEND:20251226T190000Z\n" +
                "STATUS:CANCELLED\n" +
                "END:VEVENT\n" +
                "BEGIN:VEVENT\n" +
                "UID:team-sync\n" +
                "SUMMARY:Team Sync\n" +
                "DTSTART:20251226T190000Z\n" +
                "DTEND:20251226T193000Z\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT\n" +
                "END:VCALENDAR\n";

            DateTime now = new DateTime(2025, 12, 26, 16, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> entries = CalendarService.ParseIcs(ics, now);

            DateTime cancelledUtc = new DateTime(2025, 12, 26, 18, 0, 0, DateTimeKind.Utc);
            Assert.DoesNotContain(entries, e =>
                e.Title == "Exercise @ Pinny" &&
                e.StartTime.ToUniversalTime() == cancelledUtc);

            // And ensure the next meeting is the later confirmed meeting.
            Models.CalendarEntry? next = NextMeetingSelector.GetNextMeeting(entries, now, ignoreFreeOrFollowing: false);
            Assert.NotNull(next);
            Assert.Equal("Team Sync", next!.Title);
        }

        [Fact]
        public void ParseIcs_Exdate_ExcludesCancelledOccurrence()
        {
            // Some feeds represent a cancelled single occurrence via EXDATE on the master VEVENT
            // rather than emitting a separate STATUS:CANCELLED VEVENT.
            string ics =
                "BEGIN:VCALENDAR\n" +
                "BEGIN:VEVENT\n" +
                "UID:exercise-pinny\n" +
                "SUMMARY:Exercise @ Pinny\n" +
                "DTSTART:20251219T180000Z\n" +
                "DTEND:20251219T190000Z\n" +
                "RRULE:FREQ=WEEKLY;BYDAY=FR;UNTIL=20260131T235900Z\n" +
                "EXDATE:20251226T180000Z\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT\n" +
                "BEGIN:VEVENT\n" +
                "UID:team-sync\n" +
                "SUMMARY:Team Sync\n" +
                "DTSTART:20251226T190000Z\n" +
                "DTEND:20251226T193000Z\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT\n" +
                "END:VCALENDAR\n";

            DateTime now = new DateTime(2025, 12, 26, 16, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> entries = CalendarService.ParseIcs(ics, now);

            DateTime excludedUtc = new DateTime(2025, 12, 26, 18, 0, 0, DateTimeKind.Utc);
            Assert.DoesNotContain(entries, e =>
                e.Title == "Exercise @ Pinny" &&
                e.StartTime.ToUniversalTime() == excludedUtc);

            Models.CalendarEntry? next = NextMeetingSelector.GetNextMeeting(entries, now, ignoreFreeOrFollowing: false);
            Assert.NotNull(next);
            Assert.Equal("Team Sync", next!.Title);
        }

        [Fact]
        public void ParseIcs_RecurrenceIdOverride_SuppressesOriginalInstance()
        {
            // Outlook/Exchange can emit a modified occurrence as a separate VEVENT with RECURRENCE-ID.
            // The generated instance at the RECURRENCE-ID should be suppressed.
            string ics =
                "BEGIN:VCALENDAR\n" +
                "BEGIN:VEVENT\n" +
                "UID:series-uid\n" +
                "SUMMARY:Exercise @ Pinny\n" +
                "DTSTART:20251219T180000Z\n" +
                "DTEND:20251219T190000Z\n" +
                "RRULE:FREQ=WEEKLY;BYDAY=FR;UNTIL=20260131T235900Z\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT\n" +
                // Override the 12/26 occurrence and move it earlier (12/25)
                "BEGIN:VEVENT\n" +
                "UID:series-uid\n" +
                "RECURRENCE-ID:20251226T180000Z\n" +
                "SUMMARY:Exercise @ Pinny\n" +
                "DTSTART:20251225T160000Z\n" +
                "DTEND:20251225T170000Z\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT\n" +
                "BEGIN:VEVENT\n" +
                "UID:team-sync\n" +
                "SUMMARY:Team Sync\n" +
                "DTSTART:20251226T190000Z\n" +
                "DTEND:20251226T193000Z\n" +
                "STATUS:CONFIRMED\n" +
                "END:VEVENT\n" +
                "END:VCALENDAR\n";

            DateTime now = new DateTime(2025, 12, 26, 16, 0, 0, DateTimeKind.Utc).ToLocalTime();
            IReadOnlyList<Models.CalendarEntry> entries = CalendarService.ParseIcs(ics, now);

            // The original (generated) 12/26 18:00Z should not appear.
            DateTime originalUtc = new DateTime(2025, 12, 26, 18, 0, 0, DateTimeKind.Utc);
            Assert.DoesNotContain(entries, e =>
                e.RecurrenceId is null &&
                e.Title == "Exercise @ Pinny" &&
                e.StartTime.ToUniversalTime() == originalUtc);

            // Ensure the next meeting is Team Sync.
            Models.CalendarEntry? next = NextMeetingSelector.GetNextMeeting(entries, now, ignoreFreeOrFollowing: false);
            Assert.NotNull(next);
            Assert.Equal("Team Sync", next!.Title);
        }
    }
}
