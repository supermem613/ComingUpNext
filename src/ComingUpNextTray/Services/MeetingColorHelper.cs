namespace ComingUpNextTray.Services
{
    using System.Drawing;
    using ComingUpNextTray;

    /// <summary>
    /// Helper to pick background/foreground colors for overlay and hover window based on meeting timing.
    /// </summary>
    internal static class MeetingColorHelper
    {
        /// <summary>
        /// Returns background and foreground color pair for the given state and minutes remaining (if applicable).
        /// </summary>
        /// <param name="state">The computed icon state describing meeting timing.</param>
        /// <param name="minutesRemaining">If available, the number of minutes until the next meeting start; otherwise <c>null</c>.</param>
        /// <returns>A tuple containing the background color and foreground color to use.</returns>
        internal static (Color background, Color foreground) GetColors(TrayApplication.IconState state, double? minutesRemaining)
        {
            Color bg = Color.Black;
            Color fg = Color.White;

            if (state == TrayApplication.IconState.MinutesRemaining && minutesRemaining is double m)
            {
                if (m <= 5)
                {
                    bg = Color.Red;
                }
                else if (m <= 15)
                {
                    bg = Color.Gold;
                    fg = Color.Black;
                }
                else
                {
                    bg = Color.Green;
                }
            }
            else if (state == TrayApplication.IconState.Started)
            {
                bg = Color.DarkRed;
            }
            else if (state == TrayApplication.IconState.DistantFuture)
            {
                bg = Color.MediumBlue;
            }
            else if (state == TrayApplication.IconState.NoMeeting)
            {
                bg = Color.DimGray;
            }
            else if (state == TrayApplication.IconState.NoCalendar)
            {
                bg = Color.DarkGray;
            }

            return (bg, fg);
        }
    }
}
