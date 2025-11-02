using System;
using Xunit;

namespace ComingUpNextTray.Tests {
    public class IconStateTests {
        [Fact]
        public void NoCalendar_Yields_NoCalendar_State() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_icon_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication();
                Program.TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
                Assert.Equal(ComingUpNextTray.Program.TrayApplication.IconState.NoCalendar, state);
            }
            finally {
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }

                if (File.Exists(tempPath + ".invalid")) {
                    File.Delete(tempPath + ".invalid");
                }
            }
        }

        [Fact]
        public void NoMeeting_Yields_NoMeeting_State() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_icon_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication();
                // Set a calendar URL but no meetings fetched
                System.Reflection.FieldInfo urlField = typeof(ComingUpNextTray.Program.TrayApplication).GetField("_calendarUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                urlField.SetValue(app, "https://example.com/calendar.ics");
                Program.TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
                Assert.Equal(ComingUpNextTray.Program.TrayApplication.IconState.NoMeeting, state);
            }
            finally {
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }

                if (File.Exists(tempPath + ".invalid")) {
                    File.Delete(tempPath + ".invalid");
                }
            }
        }

        [Fact]
        public void DistantFuture_Meeting_State() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_icon_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication();
                System.Reflection.FieldInfo urlField = typeof(ComingUpNextTray.Program.TrayApplication).GetField("_calendarUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                urlField.SetValue(app, "https://example.com/calendar.ics");
                System.Reflection.FieldInfo nextField = typeof(ComingUpNextTray.Program.TrayApplication).GetField("_nextMeeting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                nextField.SetValue(app, new ComingUpNextTray.Models.CalendarEntry { Title = "Far", StartTime = DateTime.Now.AddDays(2), EndTime = DateTime.Now.AddDays(2).AddHours(1) });
                Program.TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
                Assert.Equal(ComingUpNextTray.Program.TrayApplication.IconState.DistantFuture, state);
            }
            finally {
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }

                if (File.Exists(tempPath + ".invalid")) {
                    File.Delete(tempPath + ".invalid");
                }
            }
        }

        [Fact]
        public void MinutesRemaining_State() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_icon_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication();
                System.Reflection.FieldInfo urlField = typeof(ComingUpNextTray.Program.TrayApplication).GetField("_calendarUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                urlField.SetValue(app, "https://example.com/calendar.ics");
                System.Reflection.FieldInfo nextField = typeof(ComingUpNextTray.Program.TrayApplication).GetField("_nextMeeting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                nextField.SetValue(app, new ComingUpNextTray.Models.CalendarEntry { Title = "Soon", StartTime = DateTime.Now.AddMinutes(30), EndTime = DateTime.Now.AddMinutes(60) });
                Program.TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
                Assert.Equal(ComingUpNextTray.Program.TrayApplication.IconState.MinutesRemaining, state);
            }
            finally {
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }

                if (File.Exists(tempPath + ".invalid")) {
                    File.Delete(tempPath + ".invalid");
                }
            }
        }

        [Fact]
        public void Started_State() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_icon_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication();
                System.Reflection.FieldInfo urlField = typeof(ComingUpNextTray.Program.TrayApplication).GetField("_calendarUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                urlField.SetValue(app, "https://example.com/calendar.ics");
                System.Reflection.FieldInfo nextField = typeof(ComingUpNextTray.Program.TrayApplication).GetField("_nextMeeting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                nextField.SetValue(app, new ComingUpNextTray.Models.CalendarEntry { Title = "Now", StartTime = DateTime.Now.AddMinutes(-1), EndTime = DateTime.Now.AddMinutes(30) });
                Program.TrayApplication.IconState state = app.ComputeIconState(DateTime.Now);
                Assert.Equal(ComingUpNextTray.Program.TrayApplication.IconState.Started, state);
            }
            finally {
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }

                if (File.Exists(tempPath + ".invalid")) {
                    File.Delete(tempPath + ".invalid");
                }
            }
        }
    }
}
