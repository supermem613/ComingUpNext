namespace ComingUpNextTray
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using ComingUpNextTray.Models;
    using ComingUpNextTray.Services;

    /// <summary>
    /// Core tray application logic (minimal rebuild baseline). Provides computation helpers
    /// used by existing tests while full UI, timers, icon rendering, and calendar refresh
    /// functionality are reintroduced incrementally.
    /// </summary>
    internal sealed class TrayApplication : IDisposable
    {
#pragma warning disable SA1309 // Field names must not begin with underscore (tests rely on underscore names via reflection)
        // Readonly configuration path determined at construction. Tests reflect on this field name.
        private readonly string _configPath; // retained for test reflection
        private readonly bool _isTestMode;   // retained for test reflection

        // Mutable state accessed by tests via reflection (field names must remain unchanged).
        private string? _calendarUrl;        // tests set via reflection
        private CalendarEntry? _nextMeeting; // tests set via reflection
        private bool _configErrorDetected;
        private bool _disposed;
#pragma warning restore SA1309

        /// <summary>
        /// Initializes a new instance of the <see cref="TrayApplication"/> class.
        /// Establishes configuration path (AppData or test override).
        /// </summary>
        public TrayApplication()
        {
            // Initialize fields to avoid CS0649 warnings until full logic restored.
            this._calendarUrl = string.Empty;
            this._nextMeeting = null;
            this._configErrorDetected = false;
            string? overridePath = Environment.GetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                this._configPath = overridePath!;
                this._isTestMode = true;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                this._configPath = Path.Combine(appData, Program.AppFolderName, Program.ConfigFileName);
                this._isTestMode = false;
            }

            // Attempt to load configuration on startup; create folder if necessary.
            this.LoadConfig();
        }

        /// <summary>
        /// Icon rendering / state abstraction for tests.
        /// </summary>
        internal enum IconState
        {
            /// <summary>No calendar URL configured.</summary>
            NoCalendar,

            /// <summary>Calendar configured but no upcoming meeting.</summary>
            NoMeeting,

            /// <summary>Next meeting more than 24 hours away.</summary>
            DistantFuture,

            /// <summary>Meeting upcoming within the next 24 hours.</summary>
            MinutesRemaining,

            /// <summary>Meeting start time has passed.</summary>
            Started,
        }

        /// <summary>
        /// Releases resources. (Future: dispose timers/icon objects when reintroduced).
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
        }

        /// <summary>Formats minutes for icon overlay (public for test wrapper access).</summary>
        /// <param name="minutes">Minutes until meeting start.</param>
        /// <returns>Formatted compact string.</returns>
        internal static string FormatMinutesForIcon(int minutes)
        {
            if (minutes <= 0)
            {
                return UiText.ZeroMinutes;
            }

            if (minutes < 60)
            {
                return minutes.ToString(CultureInfo.InvariantCulture);
            }

            int hours = (int)Math.Round(minutes / 60.0, MidpointRounding.AwayFromZero);
            hours = Math.Clamp(hours, 1, 23);
            return hours.ToString(CultureInfo.InvariantCulture) + UiText.HourSuffix;
        }

        /// <summary>Computes current icon state based on calendar configuration and next meeting time.</summary>
        /// <param name="now">Reference time.</param>
        /// <returns>Icon state value.</returns>
        internal IconState ComputeIconState(DateTime now)
        {
            if (string.IsNullOrWhiteSpace(this._calendarUrl))
            {
                return IconState.NoCalendar;
            }

            if (this._nextMeeting is null)
            {
                return IconState.NoMeeting;
            }

            TimeSpan delta = this._nextMeeting.StartTime - now;
            if (delta.TotalMinutes >= 24 * 60)
            {
                return IconState.DistantFuture;
            }

            if (delta.TotalMinutes <= 0)
            {
                return IconState.Started;
            }

            return IconState.MinutesRemaining;
        }

        /// <summary>Builds full tooltip text (untruncated) for tests.</summary>
        /// <param name="now">Reference time.</param>
        /// <returns>Tooltip string.</returns>
        internal string BuildTooltipForTest(DateTime now)
        {
            IconState state = this.ComputeIconState(now);
            string tooltip = state switch {
                IconState.NoCalendar => UiText.NoCalendarConfigured,
                _ => NextMeetingSelector.FormatTooltip(this._nextMeeting, now),
            };
            if (state == IconState.DistantFuture)
            {
                tooltip += UiText.DistantFutureSuffix;
            }

            return tooltip;
        }

        /// <summary>Gets configuration file path (test helper).</summary>
        /// <returns>Absolute path.</returns>
        internal string GetConfigFilePathForTest() => this._configPath;

        /// <summary>Indicates if a config error was detected (test helper).</summary>
        /// <returns><c>true</c> if config error flagged.</returns>
        internal bool WasConfigErrorDetectedForTest() => this._configErrorDetected;

        /// <summary>
        /// Gets a value indicating whether the application was started in test mode (environment override).
        /// </summary>
        /// <returns><c>true</c> if test override path was supplied; otherwise <c>false</c>.</returns>
        internal bool IsTestModeForTest() => this._isTestMode;

        /// <summary>
        /// Saves the current configuration to the configuration file.
        /// </summary>
        /// <param name="config">The configuration model to save.</param>
        internal void SaveConfig(ConfigModel config)
        {
            ObjectDisposedException.ThrowIf(this._disposed, nameof(TrayApplication));

            try
            {
                string json = JsonSerializer.Serialize(config, JsonSerializerOptionsCache.IndentedOptions);
                File.WriteAllText(this._configPath, json);
            }
            catch (Exception ex)
            {
                this._configErrorDetected = true;
                throw new InvalidOperationException("Failed to save configuration.", ex);
            }
        }

        // Placeholder for legacy notification logic (balloon tip) referenced by tests via reflection.
        // Will be implemented when notification feature is reintroduced.
        // Intentionally non-static: tests obtain MethodInfo from instance type expecting instance method.
#pragma warning disable CA1822 // Mark members as static -- intentionally instance for reflection-based tests
        private void MaybeShowBalloon()
        {
        }
#pragma warning restore CA1822

        /// <summary>
        /// Loads configuration file if present. Detects malformed JSON and renames to .invalid, flagging error state.
        /// If file is absent, creates parent directory and leaves config empty.
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                string? dir = Path.GetDirectoryName(this._configPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir!);
                }

                if (!File.Exists(this._configPath))
                {
                    return; // nothing to load yet
                }

                string json = File.ReadAllText(this._configPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return; // treat empty as valid no-op
                }

                // Attempt deserialize minimal shape; ignore values if invalid types
                ConfigModel? cfg = JsonSerializer.Deserialize<ConfigModel>(json);
                if (cfg is not null)
                {
                    this._calendarUrl = cfg.CalendarUrl ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                this._configErrorDetected = true;
                try
                {
                    string invalidPath = this._configPath + UiText.InvalidSuffix;
                    if (File.Exists(invalidPath))
                    {
                        File.Delete(invalidPath); // ensure fresh rename
                    }

                    File.Move(this._configPath, invalidPath);
                }
                catch (IOException)
                {
                    // Swallow secondary IO issues; error state already flagged.
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Treat as config error to surface in tests.
                this._configErrorDetected = true;
            }
        }
    }
}
