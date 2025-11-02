using System;
using System.IO;
using Xunit;
using ComingUpNextTray;

namespace ComingUpNextTray.Tests {
    public class ConfigValidityTests {
        [Fact]
        public void EmptyConfigFile_NotMarkedInvalid() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_empty_" + Guid.NewGuid() + ".json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using (TrayApplication app = new ComingUpNextTray.TrayApplication()) { }
                File.WriteAllText(tempPath, string.Empty);
                using (TrayApplication app2 = new ComingUpNextTray.TrayApplication()) { }
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
                using (TrayApplication app = new ComingUpNextTray.TrayApplication()) { }
                File.WriteAllText(tempPath, "{}");
                using (TrayApplication app2 = new ComingUpNextTray.TrayApplication()) { }
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
