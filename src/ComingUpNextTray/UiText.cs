namespace ComingUpNextTray
{
    /// <summary>
    /// Centralizes user-facing UI text constants for potential localization and to satisfy analyzers (CA1303).
    /// </summary>
    internal static class UiText
    {
        /// <summary>Tray icon base application text.</summary>
        internal const string AppTrayText = "ComingUpNext";

        /// <summary>Application title displayed in tray icon tooltip or UI.</summary>
        internal const string ApplicationTitle = "Coming Up Next";

        /// <summary>Context menu item to open the meeting URL.</summary>
        internal const string OpenMeeting = "Open Meeting";

        /// <summary>Context menu item to open the configuration folder.</summary>
        internal const string OpenConfigFolder = "Open Config Folder";

        /// <summary>Context menu item text to refresh calendar data.</summary>
        internal const string Refresh = "Refresh";

        /// <summary>Context menu item text to set the calendar URL.</summary>
        internal const string SetCalendarUrl = "Set Calendar URL";

        /// <summary>Context menu item text to set the refresh interval minutes.</summary>
        internal const string SetRefreshMinutes = "Refresh Interval";

        /// <summary>Context menu item to open the calendar URL directly.</summary>
        internal const string OpenCalendarUrl = "Open Calendar URL";

        /// <summary>Copies the calendar URL to the clipboard.</summary>
        internal const string CopyCalendarUrl = "Copy Calendar URL";

        /// <summary>Copies the current meeting link to the clipboard.</summary>
        internal const string CopyMeetingLink = "Copy Meeting Link";

        /// <summary>Context menu item to open the configuration JSON file.</summary>
        internal const string OpenConfigFile = "Open Config File";

        /// <summary>Context menu item to toggle the hover window.</summary>
        internal const string ToggleHoverWindow = "Show Hover Window";

        /// <summary>About dialog/menu item.</summary>
        internal const string About = "About";

        /// <summary>Label prefix for version in About dialog.</summary>
        internal const string VersionLabel = "Version:";

        /// <summary>Context menu item text to exit application.</summary>
        internal const string Exit = "Exit";

        /// <summary>Generic Save button text.</summary>
        internal const string Save = "Save";

        /// <summary>Generic Cancel button text.</summary>
        internal const string Cancel = "Cancel";

        /// <summary>Label describing refresh interval input.</summary>
        internal const string RefreshIntervalLabel = "Refresh interval (minutes):";

        /// <summary>Title for configuration error message box.</summary>
        internal const string ConfigErrorTitle = "Config Error";

        /// <summary>Body text for configuration error message box.</summary>
        internal const string ConfigErrorMessage = "Config file malformed. Renamed to config.json.invalid.";

        /// <summary>Tooltip or UI text when no calendar configured.</summary>
        internal const string NoCalendarConfigured = "No calendar URL configured";

        /// <summary>Suffix displayed for meetings more than one day away.</summary>
        internal const string DistantFutureSuffix = " (>1 day)";

        /// <summary>Infinity symbol used for icon state when meeting very far away.</summary>
        internal const string InfiniteSymbol = "\u221e";

        /// <summary>Zero minutes indicator text.</summary>
        internal const string ZeroMinutes = "0";

        /// <summary>Suffix appended for hour values.</summary>
        internal const string HourSuffix = "h";

        /// <summary>Ellipsis text constant.</summary>
        internal const string Ellipsis = "...";

        /// <summary>Literal empty JSON object used for default config file creation.</summary>
        internal const string EmptyJsonObject = "{}";

        /// <summary>Suffix appended to invalid config file rename.</summary>
        internal const string InvalidSuffix = ".invalid";

        /// <summary>Balloon tip message when meeting starts within 15 minutes.</summary>
        internal const string MeetingSoonBalloon = "Meeting starts within 15 minutes.";

        /// <summary>Balloon tip message when meeting starts within 5 minutes.</summary>
        internal const string MeetingVerySoonBalloon = "Meeting starts within 5 minutes.";

        /// <summary>Text shown when there are no upcoming meetings at all.</summary>
        internal const string NoUpcomingMeetings = "No upcoming meetings";

        /// <summary>Generic network/calendar fetch error placeholder prefix.</summary>
        internal const string FetchErrorPrefix = "Error: ";
    }
}
