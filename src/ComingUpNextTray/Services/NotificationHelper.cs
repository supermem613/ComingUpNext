namespace ComingUpNextTray.Services
{
    using ComingUpNextTray;

    /// <summary>
    /// Small pure helper to determine alert stage progression and which notification (if any) to show.
    /// Extracted for unit testing.
    /// </summary>
    internal static class NotificationHelper
    {
        /// <summary>
        /// Given minutes remaining and the current alert stage, returns the new stage and the UiText message constant to show (or null if none).
        /// </summary>
        /// <param name="minutes">Minutes until meeting start (may be negative).</param>
        /// <param name="current">Current alert stage.</param>
        /// <returns>Tuple of (new stage, message key or null).
        /// If message key is null, no balloon should be shown.</returns>
        internal static (AlertStage newStage, string? message) DetermineAlertAction(double minutes, AlertStage current)
        {
            // Priority: now (<=0), then 5min, then 10min. Only advance if not already shown.
            if (minutes <= 0 && current < AlertStage.NowShown)
            {
                return (AlertStage.NowShown, UiText.MeetingNowBalloon);
            }

            if (minutes <= 5 && current < AlertStage.FiveMinutesShown)
            {
                return (AlertStage.FiveMinutesShown, UiText.MeetingVerySoonBalloon);
            }

            if (minutes <= 10 && current < AlertStage.FifteenMinutesShown)
            {
                return (AlertStage.FifteenMinutesShown, UiText.MeetingSoonBalloon);
            }

            return (current, null);
        }
    }
}
