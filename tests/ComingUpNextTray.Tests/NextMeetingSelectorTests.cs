using ComingUpNextTray.Models;
using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests;

public class NextMeetingSelectorTests
{
    [Fact]
    public void GetNextMeeting_ReturnsMeetingStartingNow()
    {
        var now = DateTime.Now;
        var meetings = new[]
        {
            new CalendarEntry { Title = "Later", StartTime = now.AddMinutes(10), EndTime = now.AddMinutes(40) },
            new CalendarEntry { Title = "Now", StartTime = now, EndTime = now.AddMinutes(30) }
        };
        var next = NextMeetingSelector.GetNextMeeting(meetings, now);
        Assert.NotNull(next);
        Assert.Equal("Now", next!.Title);
    }

    [Fact]
    public void FormatTooltip_InMinutes()
    {
        var now = DateTime.Now;
        var meeting = new CalendarEntry { Title = "Soon", StartTime = now.AddMinutes(12), EndTime = now.AddMinutes(42) };
        var text = NextMeetingSelector.FormatTooltip(meeting, now);
        Assert.Contains("In 12 min", text);
    }

    [Fact]
    public void FormatTooltip_InHoursSameDay()
    {
        var now = DateTime.Now.Date.AddHours(8); // 08:00
        var meeting = new CalendarEntry { Title = "Later Today", StartTime = now.AddHours(3), EndTime = now.AddHours(4) }; // 11:00
        var text = NextMeetingSelector.FormatTooltip(meeting, now);
        Assert.Contains("In 3 h", text);
    }

    [Fact]
    public void FormatTooltip_NextDayShowsDayTime()
    {
        var now = DateTime.Now.Date.AddHours(20); // evening today
        var meeting = new CalendarEntry { Title = "Tomorrow Meeting", StartTime = now.Date.AddDays(1).AddHours(9), EndTime = now.Date.AddDays(1).AddHours(10) }; // tomorrow 09:00
        var text = NextMeetingSelector.FormatTooltip(meeting, now);
        // Expect format like: Next: Tomorrow Meeting (Fri 09:00) depending day abbreviation
        Assert.Contains(meeting.StartTime.ToString("ddd HH:mm"), text);
    }

    [Fact]
    public void GetNextMeeting_SortsProperly()
    {
        var now = DateTime.Now;
        var meetings = new[]
        {
            new CalendarEntry { Title = "B", StartTime = now.AddMinutes(30), EndTime = now.AddMinutes(60) },
            new CalendarEntry { Title = "A", StartTime = now.AddMinutes(10), EndTime = now.AddMinutes(40) },
        };
        var next = NextMeetingSelector.GetNextMeeting(meetings, now);
        Assert.Equal("A", next!.Title);
    }
}
