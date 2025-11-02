using System;
using System.IO;
using Xunit;

namespace ComingUpNextTray.Tests {
    public class ConfigValidityTests {
        [Fact]
        public void EmptyConfigFile_NotMarkedInvalid() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_empty_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using (Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication()) { }
                File.WriteAllText(tempPath, string.Empty);
                using (Program.TrayApplication app2 = new ComingUpNextTray.Program.TrayApplication()) { }
                Assert.True(File.Exists(tempPath));
                Assert.False(File.Exists(tempPath + ".invalid"));
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
        public void EmptyObjectConfig_NotMarkedInvalid() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_emptyobj_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using (Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication()) { }
                File.WriteAllText(tempPath, "{}");
                using (Program.TrayApplication app2 = new ComingUpNextTray.Program.TrayApplication()) { }
                Assert.True(File.Exists(tempPath));
                Assert.False(File.Exists(tempPath + ".invalid"));
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
