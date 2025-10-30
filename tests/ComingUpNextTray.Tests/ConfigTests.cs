using System.Text.Json;
using Xunit;

namespace ComingUpNextTray.Tests;

public class ConfigTests
{
    private const string SampleConfig = "{\n  \"CalendarUrl\": \"https://example.com/cal.ics\",\n  \"RefreshMinutes\": 7\n}";

    [Fact]
    public void Deserialize_Config_WithRefreshMinutes()
    {
        var doc = JsonSerializer.Deserialize<ConfigModelShim>(SampleConfig);
        Assert.NotNull(doc);
        Assert.Equal("https://example.com/cal.ics", doc!.CalendarUrl);
        Assert.Equal(7, doc.RefreshMinutes);
    }

    private sealed class ConfigModelShim
    {
        public string? CalendarUrl { get; set; }
        public int? RefreshMinutes { get; set; }
    }
}