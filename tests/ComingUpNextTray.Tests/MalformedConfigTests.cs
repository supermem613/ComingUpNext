using System;
using System.IO;
using Xunit;
using ComingUpNextTray;

namespace ComingUpNextTray.Tests {
    public class MalformedConfigTests {
        [Fact]
        public void Malformed_Config_Renames_File() {
            string tempPath = Path.Combine(Path.GetTempPath(), "cun_malformed_" + Guid.NewGuid() + ".json");
            File.WriteAllText(tempPath, "{ invalid json");
            using (TrayApplication app = new TrayApplication(tempPath)) {
                Assert.True(app.WasConfigErrorDetectedForTest());
            }
            Assert.True(File.Exists(tempPath + ".invalid"));
            File.Delete(tempPath + ".invalid");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
