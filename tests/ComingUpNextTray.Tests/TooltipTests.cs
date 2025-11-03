using System;
using System.IO;
using Xunit;
using ComingUpNextTray;

namespace ComingUpNextTray.Tests {
    public class TooltipTests {
        [Fact]
        public void NoCalendar_Tooltip_Shows_Message() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_tooltip_nocal_" + Guid.NewGuid() + ".json");
            using TrayApplication app = new TrayApplication(tempPath);
            TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
            Assert.Equal(TrayApplication.IconState.NoCalendar, state);
            string tip = app.BuildTooltipForTest(DateTime.Now);
            Assert.Equal("No calendar URL configured", tip);
            if (File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        }

        [Fact]
        public void DistantFutureBalloon_AppendsHint() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_tooltip_far_" + Guid.NewGuid() + ".json");
            using TrayApplication app = new TrayApplication(tempPath);
            System.Reflection.FieldInfo urlField = typeof(TrayApplication).GetField("_calendarUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            urlField.SetValue(app, "https://example.com/cal.ics");
            System.Reflection.FieldInfo nextField = typeof(TrayApplication).GetField("_nextMeeting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            nextField.SetValue(app, new ComingUpNextTray.Models.CalendarEntry { Title = "Far", StartTime = DateTime.Now.AddDays(2), EndTime = DateTime.Now.AddDays(2).AddHours(1) });
            TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
            Assert.Equal(TrayApplication.IconState.DistantFuture, state);
            string tip = app.BuildTooltipForTest(DateTime.Now);
            Assert.Contains("(>1 day)", tip);
            if (File.Exists(tempPath)) {
                File.Delete(tempPath);
            }
        }
    }
}
