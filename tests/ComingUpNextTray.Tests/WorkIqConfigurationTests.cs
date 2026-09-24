using System.Text.Json;
using ComingUpNextTray.Models;
using Xunit;

namespace ComingUpNextTray.Tests
{
    public class WorkIqConfigurationTests
    {
        [Fact]
        public void ExistingConfigWithoutCalendarSourceDefaultsToIcsAndRetainsUrl()
        {
            const string legacyConfig = "{\"CalendarUrl\":\"https://example.com/calendar.ics\",\"Version\":1}";
            ConfigModel? config = JsonSerializer.Deserialize<ConfigModel>(legacyConfig);
            JsonElement saved = JsonSerializer.SerializeToElement(config);

            Assert.Equal(
                ("Ics", "https://example.com/calendar.ics"),
                (ReadString(saved, "CalendarSource"), ReadString(saved, "CalendarUrl")));
        }

        [Fact]
        public void WorkIqSourceRoundTripsWithoutDroppingIcsUrl()
        {
            const string selectedWorkIqConfig = "{\"CalendarUrl\":\"https://example.com/calendar.ics\",\"CalendarSource\":\"WorkIq\",\"Version\":1}";
            ConfigModel? config = JsonSerializer.Deserialize<ConfigModel>(selectedWorkIqConfig);
            JsonElement saved = JsonSerializer.SerializeToElement(config);

            Assert.Equal(
                ("WorkIq", "https://example.com/calendar.ics"),
                (ReadString(saved, "CalendarSource"), ReadString(saved, "CalendarUrl")));
        }

        [Fact]
        public void WorkIqAccountEmailRoundTripsInConfig()
        {
            const string account = "marcusm@microsoft.com";
            string selectedWorkIqConfig = JsonSerializer.Serialize(new
            {
                CalendarSource = "WorkIq",
                WorkIqAccount = account,
            });

            ConfigModel? config = JsonSerializer.Deserialize<ConfigModel>(selectedWorkIqConfig);
            JsonElement saved = JsonSerializer.SerializeToElement(config);

            Assert.Equal(account, ReadString(saved, "WorkIqAccount"));
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
