using System;
using System.IO;
using ComingUpNextTray;
using Xunit;

namespace ComingUpNextTray.Tests
{
    public class IgnoreFreeFollowingTests
    {
        [Fact]
        public void DefaultConfig_IgnoreFreeOrFollowing_IsTrue()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "config.json");
            using TrayApplication app = new TrayApplication(tempPath);
            // By default setting should be true
            Assert.True(app.GetIgnoreFreeOrFollowingForUi());
        }
    }
}
