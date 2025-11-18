using System;
using System.Drawing;
using System.Reflection;
using Xunit;
using ComingUpNextTray;

namespace ComingUpNextTray.Tests {
    public class MenuTruncationTests {
        [Fact]
        public void LongMenuText_IsTruncatedAndEscaped() {
            // Create a ContextMenuStrip to get the standard menu font
            using var menu = new System.Windows.Forms.ContextMenuStrip();
            string longText = new string('A', 1000) + " & example";
            string escaped = longText.Replace("&", "&&", StringComparison.Ordinal);

            string truncated = UiTruncation.TruncateToFit(escaped, menu.Font, UiLayout.DefaultMaxTextWidth);
            Assert.Contains("...", truncated);
            Assert.DoesNotContain("& ", truncated); // ensure ampersand escaped as && remains
        }
    }
}
