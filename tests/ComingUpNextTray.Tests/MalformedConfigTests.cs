using System;
using System.IO;
using Xunit;

namespace ComingUpNextTray.Tests {
    public class MalformedConfigTests {
        [Fact]
        public void Malformed_Config_Renames_File() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_malformed_" + Guid.NewGuid() + ".json");
            // Create malformed file BEFORE constructing app so first LoadConfig triggers rename
            File.WriteAllText(tempPath, "{ invalid json");
            Environment.SetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH", tempPath);
            try {
                using (Program.TrayApplication app = new ComingUpNextTray.Program.TrayApplication()) { }
                Assert.True(File.Exists(tempPath + ".invalid"));
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
