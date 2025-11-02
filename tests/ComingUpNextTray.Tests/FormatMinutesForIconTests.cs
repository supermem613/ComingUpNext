using System;
using Xunit;

namespace ComingUpNextTray.Tests {

    public class FormatMinutesForIconTests {
        [Theory]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        [InlineData(59, "59")]
        [InlineData(60, "1h")]
        [InlineData(61, "1h")]
        [InlineData(119, "2h")]
        [InlineData(18 * 60, "18h")]
        [InlineData(23 * 60, "23h")]
        [InlineData(24 * 60 - 1, "23h")] // capped before distant future
        public void Formats_As_Expected(int minutes, string expected) {
            string actual = ComingUpNextTray.Program.FormatMinutesForIconForTest(minutes);
            Assert.Equal(expected, actual);
        }
    }
}
