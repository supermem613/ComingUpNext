using ComingUpNextTray.Services;

namespace ComingUpNextTray.Tests
{
    using Xunit;

    public class NotificationHelperTests
    {
        [Theory]
        // Starting at None should produce 15-min message at 15
        [InlineData(14.9, 0, 1, true)]
        // If already showed 15, then 14.9 should not produce another
        [InlineData(14.9, 1, 1, false)]
        // 5 minutes threshold
        [InlineData(4.5, 0, 2, true)]
        // Already showed 5 min
        [InlineData(4.5, 2, 2, false)]
        // Now threshold (0)
        [InlineData(0.0, 0, 3, true)]
        [InlineData(-1.0, 0, 3, true)]
        // Already showed Now
        [InlineData(0.0, 3, 3, false)]
        public void DetermineAlertAction_ProgressesAsExpected(double minutes, int startStage, int expectedStage, bool expectMessage)
        {
            AlertStage start = (AlertStage)startStage;
            AlertStage expected = (AlertStage)expectedStage;
            (AlertStage newStage, string? message) = NotificationHelper.DetermineAlertAction(minutes, start);
            Assert.Equal(expected, newStage);
            if (expectMessage)
            {
                Assert.False(string.IsNullOrEmpty(message));
            }
            else
            {
                Assert.Null(message);
            }
        }
    }
}
