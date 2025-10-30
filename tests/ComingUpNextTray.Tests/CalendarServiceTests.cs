using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests;

public class CalendarServiceTests
{
    [Fact]
    public void ParseIcs_ReturnsEmpty_OnEmpty()
    {
        var result = CalendarService.ParseIcs("");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseIcs_ParsesSingleEvent()
    {
        var ics = "BEGIN:VEVENT\nSUMMARY:Test Meeting\nDTSTART:20250101T100000Z\nDTEND:20250101T103000Z\nURL:https://example.com\nEND:VEVENT";
        var result = CalendarService.ParseIcs(ics);
        var evt = Assert.Single(result);
        Assert.Equal("Test Meeting", evt.Title);
        Assert.Equal(new DateTime(2025,1,1,10,0,0,DateTimeKind.Utc).ToLocalTime().Hour, evt.StartTime.Hour);
        Assert.Equal("https://example.com", evt.MeetingUrl);
    }

    [Fact]
    public void ParseIcs_DefaultsEnd_WhenMissing()
    {
        var ics = "BEGIN:VEVENT\nSUMMARY:No End\nDTSTART:20250101T090000Z\nEND:VEVENT";
        var result = CalendarService.ParseIcs(ics);
        var evt = Assert.Single(result);
        Assert.True(evt.EndTime > evt.StartTime);
        Assert.Equal(1, (evt.EndTime - evt.StartTime).Hours);
    }

    [Fact]
    public void ParseIcs_FindsUrlInDescription()
    {
        var ics = "BEGIN:VEVENT\nSUMMARY:Desc Link\nDTSTART:20250101T090000Z\nDESCRIPTION: Join at https://example.com/meet \nEND:VEVENT";
        var result = CalendarService.ParseIcs(ics);
        var evt = Assert.Single(result);
        Assert.Equal("https://example.com/meet", evt.MeetingUrl);
    }

    [Fact]
    public void ParseIcs_HandlesLineFolding()
    {
        // DESCRIPTION line folded (second line starts with space)
        var ics = "BEGIN:VEVENT\nSUMMARY:Folded Meeting\nDTSTART:20250101T100000Z\nDESCRIPTION: First part of description\n continuation with https://example.com/folded \nEND:VEVENT";
        var result = CalendarService.ParseIcs(ics);
        var evt = Assert.Single(result);
        Assert.Equal("Folded Meeting", evt.Title);
        Assert.Equal("https://example.com/folded", evt.MeetingUrl);
    }

    [Fact]
    public void ParseIcs_AllDayEvent_DateOnly()
    {
        var ics = "BEGIN:VEVENT\nSUMMARY:All Day Event\nDTSTART:20250102\nEND:VEVENT"; // DTEND omitted
        var result = CalendarService.ParseIcs(ics);
        var evt = Assert.Single(result);
        Assert.Equal(new DateTime(2025,1,2), evt.StartTime.Date);
        Assert.Equal(1, (evt.EndTime - evt.StartTime).Hours); // default duration
    }

    [Fact]
    public void ParseIcs_SkipsMalformed_NoStart()
    {
        var ics = "BEGIN:VEVENT\nSUMMARY:No Start Provided\nEND:VEVENT";
        var result = CalendarService.ParseIcs(ics);
        Assert.Empty(result); // should skip since no DTSTART
    }
}