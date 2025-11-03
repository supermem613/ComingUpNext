using System;
using ComingUpNextTray.Models;
using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests {
    public class SecondMeetingTests {
        [Fact]
        public void GetSecondMeeting_ReturnsSecondUpcoming() {
            DateTime now = DateTime.Now;
            CalendarEntry[] meetings = new[] {
                new CalendarEntry { Title = "First", StartTime = now.AddMinutes(10), EndTime = now.AddMinutes(40) },
                new CalendarEntry { Title = "Second", StartTime = now.AddMinutes(50), EndTime = now.AddMinutes(80) },
                new CalendarEntry { Title = "Third", StartTime = now.AddMinutes(120), EndTime = now.AddMinutes(150) },
            };
            // Simulate application state.
            using ComingUpNextTray.TrayApplication app = new ComingUpNextTray.TrayApplication();
            // Access private calendar service not feasible; we mimic by reflection injecting _lastEntries and _nextMeeting.
            Type appType = typeof(ComingUpNextTray.TrayApplication);
            System.Reflection.FieldInfo? lastEntriesField = appType.GetField("_lastEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo? nextField = appType.GetField("_nextMeeting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(lastEntriesField);
            Assert.NotNull(nextField);
            lastEntriesField!.SetValue(app, meetings);
            nextField!.SetValue(app, NextMeetingSelector.GetNextMeeting(meetings, now));

            CalendarEntry? second = app.GetSecondMeetingForUi();
            Assert.NotNull(second);
            Assert.Equal("Second", second!.Title);
        }
    }
}
