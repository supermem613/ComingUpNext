using System;
using System.IO;
using Xunit;

namespace ComingUpNextTray.Tests {
    public class AppDataConfigPathTests {
        [Fact]
        public void Config_Path_Is_In_AppData_Roaming() {
            string? overridePath = Environment.GetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath)) {
                // If an override is set (other test leaked), clear and proceed
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
            }
            using Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication();
            string path = app.GetConfigFilePathForTest();
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Assert.StartsWith(roaming, Path.GetDirectoryName(path)!);
            Assert.EndsWith(Path.Combine("ComingUpNext", "config.json"), path.Replace(roaming + Path.DirectorySeparatorChar, string.Empty));
        }

        [Fact]
        public void Save_Creates_File_In_AppData() {
            string? overridePath = Environment.GetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath)) {
                Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", null);
            }

            using Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication();
            string path = app.GetConfigFilePathForTest();
            if (File.Exists(path)) {
                File.Delete(path);
            }

            app.SaveConfig();
            Assert.True(File.Exists(path));
        }
    }
}
