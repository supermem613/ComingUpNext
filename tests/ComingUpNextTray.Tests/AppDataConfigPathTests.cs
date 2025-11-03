using System;
using System.IO;
using Xunit;
using ComingUpNextTray;
using ComingUpNextTray.Models;

namespace ComingUpNextTray.Tests {
    public class AppDataConfigPathTests {
        [Fact]
        public void Config_Path_Is_In_Install_Directory() {
            using TrayApplication app = new TrayApplication();
            string path = app.GetConfigFilePathForTest();
            string? actualDir = Path.GetFullPath(Path.GetDirectoryName(path)!);
            string expectedDir = Path.GetFullPath(AppContext.BaseDirectory);
            Assert.Equal(expectedDir.TrimEnd(System.IO.Path.DirectorySeparatorChar), actualDir.TrimEnd(System.IO.Path.DirectorySeparatorChar));
            Assert.Equal("config.json", Path.GetFileName(path));
        }

        [Fact]
        public void Save_Creates_File_In_Explicit_Path() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_override_" + Guid.NewGuid() + ".json");
            using TrayApplication app = new TrayApplication(tempPath);
            string path = app.GetConfigFilePathForTest();
            Assert.Equal(tempPath, path);
            if (File.Exists(path)) {
                File.Delete(path);
            }

            ConfigModel config = new ConfigModel {
                CalendarUrl = "https://example.com/calendar.ics",
                RefreshMinutes = 15
            };

            app.SaveConfig(config);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }
    }
}
