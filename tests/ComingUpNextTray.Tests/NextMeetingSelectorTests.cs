using ComingUpNextTray.Models;
using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests {
    public class NextMeetingSelectorTests {
        [Fact]
        public void GetNextMeeting_ReturnsMeetingStartingNow() {
            DateTime now = DateTime.Now;
            CalendarEntry[] meetings = new[]
            {
                new CalendarEntry { Title = "Later", StartTime = now.AddMinutes(10), EndTime = now.AddMinutes(40) },
                new CalendarEntry { Title = "Now", StartTime = now, EndTime = now.AddMinutes(30) }
            };
            CalendarEntry? next = NextMeetingSelector.GetNextMeeting(meetings, now);
            Assert.NotNull(next);
            Assert.Equal("Now", next!.Title);
        }

        [Fact]
        public void FormatTooltip_InMinutes() {
            DateTime now = DateTime.Now;
            CalendarEntry meeting = new CalendarEntry { Title = "Soon", StartTime = now.AddMinutes(12), EndTime = now.AddMinutes(42) };
            string text = NextMeetingSelector.FormatTooltip(meeting, now);
            Assert.Contains("In 12 min", text);
        }

        [Fact]
        public void FormatTooltip_InHoursSameDay() {
            DateTime now = DateTime.Now.Date.AddHours(8); // 08:00
            CalendarEntry meeting = new CalendarEntry { Title = "Later Today", StartTime = now.AddHours(3), EndTime = now.AddHours(4) }; // 11:00
            string text = NextMeetingSelector.FormatTooltip(meeting, now);
            Assert.Contains("In 3 h", text);
        }

        [Fact]
        public void FormatTooltip_NextDayShowsDayTime() {
            DateTime now = DateTime.Now.Date.AddHours(20); // evening today
            CalendarEntry meeting = new CalendarEntry { Title = "Tomorrow Meeting", StartTime = now.Date.AddDays(1).AddHours(9), EndTime = now.Date.AddDays(1).AddHours(10) }; // tomorrow 09:00
            string text = NextMeetingSelector.FormatTooltip(meeting, now);
            // Expect format like: Next: Tomorrow Meeting (Fri 09:00) depending day abbreviation
            Assert.Contains(meeting.StartTime.ToString("ddd HH:mm"), text);
        }

        [Fact]
        public void GetNextMeeting_SortsProperly() {
            DateTime now = DateTime.Now;
            CalendarEntry[] meetings = new[]
            {
                new CalendarEntry { Title = "B", StartTime = now.AddMinutes(30), EndTime = now.AddMinutes(60) },
                new CalendarEntry { Title = "A", StartTime = now.AddMinutes(10), EndTime = now.AddMinutes(40) },
            };
            CalendarEntry? next = NextMeetingSelector.GetNextMeeting(meetings, now);
            Assert.Equal("A", next!.Title);
        }

        [Theory]
        [InlineData(-1, "Red", "White")]
        [InlineData(0, "Red", "White")]
        [InlineData(1, "Red", "White")]
        [InlineData(4, "Red", "White")]
        [InlineData(5, "Gold", "Black")]
        [InlineData(14, "Gold", "Black")]
        [InlineData(15, "Green", "White")]
        [InlineData(120, "Green", "White")]
        public void GetColorsForMinutes_Thresholds(int minutes, string expectedBgName, string expectedFgName) {
            (System.Drawing.Color bg, System.Drawing.Color fg) = ComingUpNextTray.Program.GetColorsForMinutesForTest(minutes);
            Assert.Equal(expectedBgName, bg.Name);
            Assert.Equal(expectedFgName, fg.Name);
        }

        [Theory]
        [InlineData(-5, "0")] // started
        [InlineData(0, "0")] // now
        [InlineData(1, "1")] // minute
        [InlineData(59, "59")] // under hour
        [InlineData(60, "1h")] // exactly hour
        [InlineData(119, "2h")] // ~2h
        [InlineData(120, "2h")] // 2h
        [InlineData(600, "10h")] // 10h
        [InlineData(1439, "23h")] // capped before distant future state
        public void FormatMinutesForIcon_Cases(int minutes, string expected) {
            string actual = ComingUpNextTray.Program.FormatMinutesForIconForTest(minutes);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Infinity_Not_Represented_By_FormatMinutesForIcon() {
            // FormatMinutesForIcon never returns infinity symbol; handled upstream.
            string val = ComingUpNextTray.Program.FormatMinutesForIconForTest(2000);
            Assert.NotEqual("∞", val);
        }
    }
}
