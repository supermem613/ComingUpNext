using System;
using System.IO;
using Xunit;
using ComingUpNextTray;

namespace ComingUpNextTray.Tests {
    public class TooltipTests {
        [Fact]
        public void NoCalendar_Tooltip_Shows_Message() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_tooltip_nocal_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            using TrayApplication app = new TrayApplication();
            TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
            Assert.Equal(TrayApplication.IconState.NoCalendar, state);
            string tip = app.BuildTooltipForTest(DateTime.Now);
            Assert.Equal("No calendar URL configured", tip);
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
            if (File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        }

        [Fact]
        public void DistantFutureBalloon_AppendsHint() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_tooltip_far_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            using TrayApplication app = new TrayApplication();
            System.Reflection.FieldInfo urlField = typeof(TrayApplication).GetField("_calendarUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            urlField.SetValue(app, "https://example.com/cal.ics");
            System.Reflection.FieldInfo nextField = typeof(TrayApplication).GetField("_nextMeeting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            nextField.SetValue(app, new ComingUpNextTray.Models.CalendarEntry { Title = "Far", StartTime = DateTime.Now.AddDays(2), EndTime = DateTime.Now.AddDays(2).AddHours(1) });
            // Directly call MaybeShowBalloon via reflection to avoid waiting for timer
            System.Reflection.MethodInfo maybeMethod = typeof(TrayApplication).GetMethod("MaybeShowBalloon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            maybeMethod.Invoke(app, null);
            TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
            Assert.Equal(TrayApplication.IconState.DistantFuture, state);
            string tip = app.BuildTooltipForTest(DateTime.Now);
            Assert.Contains("(>1 day)", tip);
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
            if (File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        }
    }
}
