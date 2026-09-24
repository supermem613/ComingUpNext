using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using ComingUpNextTray;
using Xunit;

namespace ComingUpNextTray.Tests
{
    public class WorkIqSettingsFormTests
    {
        [Fact]
        public void SelectingWorkIqDisablesIcsUrlAndEnablesAccountEmail()
        {
            string configPath = Path.Combine(Path.GetTempPath(), "cun_workiq_" + Guid.NewGuid() + ".json");

            try
            {
                File.WriteAllText(configPath, "{\"CalendarUrl\":\"https://example.com/calendar.ics\"}");
                (bool IcsEnabled, bool AccountEnabled) state = RunOnSta(() =>
                {
                    using TrayApplication app = new TrayApplication(configPath);
                    using SettingsForm form = new SettingsForm();
                    form.Initialize(app);

                    ComboBox source = Assert.IsType<ComboBox>(Assert.Single(form.Controls.Find("comboCalendarSource", searchAllChildren: true)));
                    TextBox calendarUrl = Assert.IsType<TextBox>(Assert.Single(form.Controls.Find("textCalendarUrl", searchAllChildren: true)));
                    TextBox account = Assert.IsType<TextBox>(Assert.Single(form.Controls.Find("textWorkIqAccount", searchAllChildren: true)));
                    source.SelectedIndex = 1;

                    return (calendarUrl.Enabled, account.Enabled);
                });

                Assert.Equal((false, true), state);
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void SavingWorkIqSettingsPersistsAccountAndRetainsIcsUrl()
        {
            const string calendarUrl = "https://example.com/calendar.ics";
            const string accountEmail = "marcusm@microsoft.com";
            string configPath = Path.Combine(Path.GetTempPath(), "cun_workiq_" + Guid.NewGuid() + ".json");

            try
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(new { CalendarUrl = calendarUrl }));
                string savedConfig = RunOnSta(() =>
                {
                    using TrayApplication app = new TrayApplication(configPath);
                    using SettingsForm form = new SettingsForm();
                    form.Initialize(app);

                    ComboBox source = Assert.IsType<ComboBox>(Assert.Single(form.Controls.Find("comboCalendarSource", searchAllChildren: true)));
                    TextBox account = Assert.IsType<TextBox>(Assert.Single(form.Controls.Find("textWorkIqAccount", searchAllChildren: true)));
                    Button save = Assert.IsType<Button>(Assert.Single(form.Controls.Find("buttonSave", searchAllChildren: true)));

                    source.SelectedIndex = 1;
                    account.Text = accountEmail;
                    form.Show();
                    save.PerformClick();

                    return File.ReadAllText(configPath);
                });

                using JsonDocument saved = JsonDocument.Parse(savedConfig);
                Assert.Equal(
                    ("WorkIq", accountEmail, calendarUrl),
                    (
                        ReadString(saved.RootElement, "CalendarSource"),
                        ReadString(saved.RootElement, "WorkIqAccount"),
                        ReadString(saved.RootElement, "CalendarUrl")));
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        private static T RunOnSta<T>(Func<T> action)
        {
            T? result = default;
            Exception? exception = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return result!;
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
