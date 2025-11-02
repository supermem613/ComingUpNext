using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ComingUpNextTray.Tests")]

namespace ComingUpNextTray
{
    /// <summary>
    /// Entry point host and test helper wrappers.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Name of the application folder under AppData\Roaming used to store configuration.
        /// </summary>
        internal const string AppFolderName = "ComingUpNext";

        /// <summary>
        /// Name of the configuration JSON file persisted to disk.
        /// </summary>
        internal const string ConfigFileName = "config.json";

        /// <summary>
        /// Test helper wrapper for icon minute formatting logic.
        /// </summary>
        /// <param name="minutes">Minutes until meeting start.</param>
        /// <returns>Formatted string for icon overlay.</returns>
        public static string FormatMinutesForIconForTest(int minutes) => TrayApplication.FormatMinutesForIcon(minutes);

        /// <summary>
        /// Test helper wrapper to compute default AppData config path without constructing application.
        /// </summary>
        /// <returns>Full expected config file path.</returns>
        public static string GetConfigFilePathForTest()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppFolderName, ConfigFileName);
        }

        /// <summary>
        /// Test helper to get colors for minutes thresholds.
        /// </summary>
        /// <param name="minutes">Minutes until meeting start.</param>
        /// <returns>Tuple of background and foreground colors.</returns>
        public static (System.Drawing.Color bg, System.Drawing.Color fg) GetColorsForMinutesForTest(int minutes)
        {
            if (minutes <= 4)
            {
                return (System.Drawing.Color.Red, System.Drawing.Color.White);
            }
            else if (minutes <= 14)
            {
                return (System.Drawing.Color.Gold, System.Drawing.Color.Black);
            }
            else
            {
                return (System.Drawing.Color.Green, System.Drawing.Color.White);
            }
        }

        /// <summary>
        /// Application entry point.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            using TrayApplication trayApp = new TrayApplication();
            Application.Run();
        }
    }
}
