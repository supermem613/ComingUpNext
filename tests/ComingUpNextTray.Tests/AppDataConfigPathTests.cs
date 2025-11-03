using System;
using System.IO;
using Xunit;
using ComingUpNextTray;
using ComingUpNextTray.Models;

namespace ComingUpNextTray.Tests {
    public class AppDataConfigPathTests {
        [Fact]
        public void Config_Path_Is_In_Install_Directory() {
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
            using TrayApplication app = new TrayApplication();
            string path = app.GetConfigFilePathForTest();
            Assert.StartsWith(AppContext.BaseDirectory, Path.GetDirectoryName(path)!);
            Assert.EndsWith("config.json", Path.GetFileName(path));
        }

        [Fact]
        public void Legacy_AppData_Config_Is_Migrated() {
            // Arrange: create a legacy AppData config if not present.
            string legacyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Program.AppFolderName);
            Directory.CreateDirectory(legacyDir);
            string legacyFile = Path.Combine(legacyDir, Program.ConfigFileName);
            File.WriteAllText(legacyFile, "{\"CalendarUrl\":\"https://legacy.example/ics\",\"RefreshMinutes\":10}");

            // Ensure destination does not exist.
            string installFile = Path.Combine(AppContext.BaseDirectory, Program.ConfigFileName);
            if (File.Exists(installFile))
            {
                File.Delete(installFile);
            }

            using TrayApplication app = new TrayApplication();
            string path = app.GetConfigFilePathForTest();
            Assert.Equal(installFile, path);
            Assert.True(File.Exists(path)); // migrated
            string json = File.ReadAllText(path);
            Assert.Contains("legacy.example", json);
        }

        [Fact]
        public void Save_Creates_File_In_Override_Path_Isolated() {
            // Use a temp override path to avoid writing into the user's real AppData config.
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_override_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using TrayApplication app = new TrayApplication();
                string path = app.GetConfigFilePathForTest();
                Assert.Equal(tempPath, path); // ensure override applied
                if (File.Exists(path)) {
                    File.Delete(path);
                }

                ConfigModel config = new ConfigModel {
                    CalendarUrl = "https://example.com/calendar.ics",
                    RefreshMinutes = 15
                };

                app.SaveConfig(config);
                Assert.True(File.Exists(path));
            }
            finally {
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
