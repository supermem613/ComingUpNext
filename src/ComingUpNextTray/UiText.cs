namespace ComingUpNextTray
{
    /// <summary>
    /// Centralizes user-facing UI text constants for potential localization and to satisfy analyzers (CA1303).
    /// </summary>
    internal static class UiText
    {
        /// <summary>Tray icon base application text.</summary>
        internal const string AppTrayText = "ComingUpNext";

        /// <summary>Context menu item to open the meeting URL.</summary>
        internal const string OpenMeeting = "Open Meeting";

        /// <summary>Context menu item to open the configuration folder.</summary>
        internal const string OpenConfigFolder = "Open Config Folder";

        /// <summary>Context menu item text to refresh calendar data.</summary>
        internal const string Refresh = "Refresh";

        /// <summary>Context menu item text to set the calendar URL.</summary>
        internal const string SetCalendarUrl = "Set Calendar URL";

        /// <summary>Context menu item text to set the refresh interval minutes.</summary>
        internal const string SetRefreshMinutes = "Set Refresh Minutes";

        /// <summary>Context menu item text to exit application.</summary>
        internal const string Exit = "Exit";

        /// <summary>Generic Save button text.</summary>
        internal const string Save = "Save";

        /// <summary>Generic Cancel button text.</summary>
        internal const string Cancel = "Cancel";

        /// <summary>Label describing refresh interval input.</summary>
        internal const string RefreshIntervalLabel = "Refresh interval (minutes):";

        /// <summary>Tooltip fragment indicating a meeting is soon.</summary>
        internal const string MeetingSoon = "Meeting soon";

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
    }
}
