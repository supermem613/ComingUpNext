namespace ComingUpNextTray
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using ComingUpNextTray.Models;
    using ComingUpNextTray.Services;

    /// <summary>Core tray application logic providing meeting refresh and formatting helpers.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:Field names should not begin with an underscore", Justification = "Private field underscore naming is intentional and conventional.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "Legacy compatibility method placement.")]
    internal sealed class TrayApplication : IDisposable
    {
        private readonly string _configPath;
        private readonly CalendarService _calendarService = new CalendarService();
        private string? _calendarUrl;
        private CalendarEntry? _nextMeeting;
        private IReadOnlyList<CalendarEntry>? _lastEntries; // cached entries from last fetch for advancement
        private bool _configErrorDetected;
        private bool _disposed;
        private DateTime _lastRefreshUtc;
        private int _refreshMinutes = 5;
        private bool _showHoverWindow = true;
        private bool _ignoreFreeOrFollowing = true;
        private string? _lastFetchError;
        private int? _hoverWindowLeft;
        private int? _hoverWindowTop;
        private int? _hoverWindowWidth;
        private int? _hoverWindowHeight;

        // Overlay is now always enabled; legacy flag retained only for backward compatible config file reads.

        /// <summary>Initializes a new instance of the <see cref="TrayApplication"/> class.</summary>
        public TrayApplication()
            : this(Path.Combine(AppContext.BaseDirectory, Program.ConfigFileName))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrayApplication"/> class for a specific config path (used by tests).
        /// </summary>
        /// <param name="configPath">Absolute path to configuration JSON.</param>
        internal TrayApplication(string configPath)
        {
            this._calendarUrl = string.Empty;
            this._nextMeeting = null;
            this._configErrorDetected = false;
            this._configPath = configPath;
            this.LoadConfig();
        }

        /// <summary>
        /// Enumeration of icon display states for the tray overlay and tooltip.
        /// </summary>
        internal enum IconState
        {
            /// <summary>No calendar URL is configured.</summary>
            NoCalendar,

            /// <summary>Calendar available but no upcoming meeting entries.</summary>
            NoMeeting,

            /// <summary>Next meeting starts more than 24 hours from now.</summary>
            DistantFuture,

            /// <summary>Meeting is upcoming and minutes remain until start.</summary>
            MinutesRemaining,

            /// <summary>The start time has passed (meeting has begun).</summary>
            Started,
        }

        /// <summary>
        /// Indicates whether the application is in legacy test mode (always false after legacy removal).
        /// </summary>
        /// <returns><c>false</c> always; retained for test compatibility.</returns>
        internal static bool IsTestModeForTest() => false;

        /// <summary>Disposes managed resources.</summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            this._calendarService.Dispose();
        }

        /// <summary>
        /// Formats a number of minutes until the next meeting into compact overlay text.
        /// For 1-59 minutes returns the minute value; for 60+ rounds to hours and appends an hour suffix.
        /// </summary>
        /// <param name="minutes">Minutes remaining until meeting start.</param>
        /// <returns>Overlay text (e.g. "5", "1h", "0").</returns>
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

        /// <summary>
        /// Fetches the calendar data (if a URL is configured) and computes the next meeting.
        /// </summary>
        /// <param name="ct">Cancellation token for aborting network I/O.</param>
        /// <returns><c>true</c> if refresh succeeded; otherwise <c>false</c>.</returns>
        internal async Task<bool> RefreshAsync(CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(this._disposed, nameof(TrayApplication));
            if (string.IsNullOrWhiteSpace(this._calendarUrl))
            {
                this._lastFetchError = null;
                return false;
            }

            try
            {
                // Try the error-propagating fetch so we can show users what went wrong.
                IReadOnlyList<CalendarEntry> newEntries = Array.Empty<CalendarEntry>();
                bool changed = false;
                if (Uri.TryCreate(this._calendarUrl, UriKind.Absolute, out Uri? uri))
                {
                    // Prefer conditional fetch when we already have validators; fall back to full fetch otherwise.
                    if (this._calendarService.HasChangeValidators)
                    {
                        newEntries = await this._calendarService.FetchIfChangedWithErrorsAsync(uri, ct).ConfigureAwait(false);
                        changed = newEntries.Count > 0; // empty list means not modified (or error not thrown).
                        if (!changed && this._lastEntries != null)
                        {
                            newEntries = this._lastEntries; // reuse prior parsed entries.
                        }
                    }
                    else
                    {
                        newEntries = await this._calendarService.FetchWithErrorsAsync(uri, ct).ConfigureAwait(false);
                        changed = true;
                    }
                }

                this._lastFetchError = null;
                this._lastEntries = newEntries;
                this._nextMeeting = NextMeetingSelector.GetNextMeeting(newEntries, DateTime.Now, this._ignoreFreeOrFollowing);
                this._lastRefreshUtc = DateTime.UtcNow;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                // Network/HTTP error
                this._lastFetchError = ex.Message;
                this._lastEntries = Array.Empty<CalendarEntry>();
                this._nextMeeting = null;
                return false;
            }
            catch (System.Text.Json.JsonException ex)
            {
                // Parsing/format errors
                this._lastFetchError = ex.Message;
                this._lastEntries = Array.Empty<CalendarEntry>();
                this._nextMeeting = null;
                return false;
            }
        }

        /// <summary>
        /// Computes the current <see cref="IconState"/> given the configured calendar and next meeting time relative to <paramref name="now"/>.
        /// </summary>
        /// <param name="now">Reference timestamp to compare against meeting start.</param>
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
            if (delta.TotalMinutes >= 1440)
            {
                return IconState.DistantFuture;
            }

            if (delta.TotalMinutes <= 0)
            {
                return IconState.Started;
            }

            return IconState.MinutesRemaining;
        }

        /// <summary>
        /// Builds full tooltip text for tests or UI consumption describing current state and meeting timing.
        /// </summary>
        /// <param name="now">Reference timestamp.</param>
        /// <returns>Tooltip text (may be truncated for NotifyIcon display).</returns>
        internal string BuildTooltipForTest(DateTime now)
        {
            IconState state = this.ComputeIconState(now);
            string tooltip;
            if (!string.IsNullOrEmpty(this._lastFetchError))
            {
                tooltip = UiText.FetchErrorPrefix + this._lastFetchError;
            }
            else
            {
                tooltip = state == IconState.NoCalendar ? UiText.NoCalendarConfigured : NextMeetingSelector.FormatTooltip(this._nextMeeting, now);
            }

            if (state == IconState.DistantFuture)
            {
                tooltip += UiText.DistantFutureSuffix;
            }

            return tooltip;
        }

        /// <summary>
        /// Gets the short overlay text to draw on the tray icon representing minutes or status.
        /// </summary>
        /// <param name="now">Reference timestamp.</param>
        /// <param name="includeUnit">If true, append a human-friendly unit to plain minute tokens (e.g. "5" -> "5 min").</param>
        /// <returns>Overlay text token.</returns>
        internal string GetOverlayText(DateTime now, bool includeUnit = false)
        {
            IconState state = this.ComputeIconState(now);
            bool tokenIsPlainMinutes = false;
            string token;

            switch (state)
            {
                case IconState.NoCalendar:
                    token = "?";
                    break;
                case IconState.NoMeeting:
                    token = "-";
                    break;
                case IconState.DistantFuture:
                    token = UiText.InfiniteSymbol;
                    break;
                case IconState.Started:
                    token = UiText.NowLabel;
                    break;
                case IconState.MinutesRemaining:
                    if (this._nextMeeting is null)
                    {
                        token = UiText.NowLabel;
                    }
                    else
                    {
                        int minutes = (int)Math.Ceiling((this._nextMeeting.StartTime - now).TotalMinutes);
                        token = FormatMinutesForIcon(minutes);

                        // If FormatMinutesForIcon chose a plain minute value (1-59) then includeUnit should append " min".
                        tokenIsPlainMinutes = minutes > 0 && minutes < 60;
                    }

                    break;
                default:
                    token = UiText.ZeroMinutes;
                    break;
            }

            if (includeUnit && tokenIsPlainMinutes)
            {
                return token + " min";
            }

            return token;
        }

        /// <summary>Gets the configuration file path for test assertions.</summary>
        /// <returns>Absolute configuration file path.</returns>
        internal string GetConfigFilePathForTest() => this._configPath;

        /// <summary>Indicates whether a configuration error was detected during load.</summary>
        /// <returns><c>true</c> if a config error occurred.</returns>
        internal bool WasConfigErrorDetectedForTest() => this._configErrorDetected;

        /// <summary>Indicates whether the application is running in test mode (override path used).</summary>
        /// <returns><c>true</c> if test mode.</returns>

        /// <summary>Gets the next meeting for UI display (may be null).</summary>
        /// <returns>The next meeting or <c>null</c>.</returns>
        internal CalendarEntry? GetNextMeetingForUi() => this._nextMeeting;

        /// <summary>Gets the second upcoming meeting (the one immediately after the next), if available.</summary>
        /// <returns>The second meeting or <c>null</c>.</returns>
        internal CalendarEntry? GetSecondMeetingForUi()
        {
            if (this._lastEntries is null)
            {
                return null;
            }

            DateTime now = DateTime.Now;

            // Ensure _nextMeeting is aligned; if not, compute using the ignore-free/following setting.
            CalendarEntry? first = this._nextMeeting ?? NextMeetingSelector.GetNextMeeting(this._lastEntries, now, this._ignoreFreeOrFollowing);
            if (first is null)
            {
                return null;
            }

            // Compute second by selecting the next entry after 'first' while also applying the ignore filter.
            IEnumerable<CalendarEntry> candidates = this._lastEntries.Where(e => e.StartTime >= now && e != first);
            return NextMeetingSelector.GetNextMeeting(candidates, now, this._ignoreFreeOrFollowing);
        }

        /// <summary>
        /// Advances to the next meeting from the cached list if the current meeting has ended.
        /// </summary>
        /// <param name="now">Current timestamp.</param>
        internal void AdvanceMeetingIfEnded(DateTime now)
        {
            if (this._nextMeeting is null || this._lastEntries is null)
            {
                return;
            }

            if (now >= this._nextMeeting.EndTime)
            {
                this._nextMeeting = NextMeetingSelector.GetNextMeeting(this._lastEntries, now, this._ignoreFreeOrFollowing);
            }
        }

        /// <summary>Gets the configured calendar URL.</summary>
        /// <returns>The calendar URL or empty string.</returns>
        internal string GetCalendarUrlForUi() => this._calendarUrl ?? string.Empty;

        /// <summary>Gets the UTC timestamp when the calendar was last refreshed.</summary>
        /// <returns>Refresh UTC timestamp.</returns>
        internal DateTime GetLastRefreshUtcForUi() => this._lastRefreshUtc;

        /// <summary>
        /// Gets the last fetch error message, if any, for UI display.
        /// </summary>
        /// <returns>Error message or null.</returns>
        internal string? GetLastFetchErrorForUi() => this._lastFetchError;

        /// <summary>Gets configured refresh interval.</summary>
        /// <returns>Minutes.</returns>
        internal int GetRefreshMinutesForUi() => this._refreshMinutes;

        /// <summary>Updates the calendar URL and saves config.</summary>
        /// <param name="url">New calendar URL (absolute) or empty to clear.</param>
        internal void SetCalendarUrl(string? url)
        {
            this._calendarUrl = string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();

            // Save full config to preserve other values
            this.SaveConfig(new ConfigModel { CalendarUrl = this._calendarUrl, RefreshMinutes = this._refreshMinutes, ShowHoverWindow = this._showHoverWindow, IgnoreFreeOrFollowing = this._ignoreFreeOrFollowing, HoverWindowLeft = this._hoverWindowLeft, HoverWindowTop = this._hoverWindowTop, HoverWindowWidth = this._hoverWindowWidth, HoverWindowHeight = this._hoverWindowHeight });
        }

        /// <summary>Sets refresh interval minutes and persists config.</summary>
        /// <param name="minutes">Minutes (1-1440).</param>
        internal void SetRefreshMinutes(int minutes)
        {
            if (minutes < 1 || minutes > 1440)
            {
                return;
            }

            this._refreshMinutes = minutes;
            this.SaveConfig(new ConfigModel { CalendarUrl = this._calendarUrl, RefreshMinutes = this._refreshMinutes, ShowHoverWindow = this._showHoverWindow, IgnoreFreeOrFollowing = this._ignoreFreeOrFollowing, HoverWindowLeft = this._hoverWindowLeft, HoverWindowTop = this._hoverWindowTop, HoverWindowWidth = this._hoverWindowWidth, HoverWindowHeight = this._hoverWindowHeight });
        }

        /// <summary>Gets whether to show the hover window.</summary>
        /// <returns>True if hover window should be shown.</returns>
        internal bool GetShowHoverWindowForUi() => this._showHoverWindow;

        /// <summary>Sets whether to show the hover window and persists config.</summary>
        /// <param name="v">New show hover window value.</param>
        internal void SetShowHoverWindow(bool v)
        {
            this._showHoverWindow = v;
            this.SaveConfig(new ConfigModel { CalendarUrl = this._calendarUrl, RefreshMinutes = this._refreshMinutes, ShowHoverWindow = this._showHoverWindow, IgnoreFreeOrFollowing = this._ignoreFreeOrFollowing, HoverWindowLeft = this._hoverWindowLeft, HoverWindowTop = this._hoverWindowTop, HoverWindowWidth = this._hoverWindowWidth, HoverWindowHeight = this._hoverWindowHeight });
        }

        /// <summary>
        /// Gets saved hover window left coordinate.
        /// </summary>
        /// <returns>Left coordinate in screen pixels, or null if not set.</returns>
        internal int? GetHoverWindowLeftForUi() => this._hoverWindowLeft;

        /// <summary>
        /// Gets saved hover window top coordinate.
        /// </summary>
        /// <returns>Top coordinate in screen pixels, or null if not set.</returns>
        internal int? GetHoverWindowTopForUi() => this._hoverWindowTop;

        /// <summary>Gets saved hover window width in pixels.</summary>
        /// <returns>Width in pixels or null.</returns>
        internal int? GetHoverWindowWidthForUi() => this._hoverWindowWidth;

        /// <summary>Gets saved hover window height in pixels.</summary>
        /// <returns>Height in pixels or null.</returns>
        internal int? GetHoverWindowHeightForUi() => this._hoverWindowHeight;

        /// <summary>
        /// Sets hover window position and persists config.
        /// </summary>
        /// <param name="left">Left coordinate in screen pixels, or null to clear.</param>
        /// <param name="top">Top coordinate in screen pixels, or null to clear.</param>
        internal void SetHoverWindowPosition(int? left, int? top)
        {
            this._hoverWindowLeft = left;
            this._hoverWindowTop = top;
            this.SaveConfig(new ConfigModel { CalendarUrl = this._calendarUrl, RefreshMinutes = this._refreshMinutes, ShowHoverWindow = this._showHoverWindow, IgnoreFreeOrFollowing = this._ignoreFreeOrFollowing, HoverWindowLeft = this._hoverWindowLeft, HoverWindowTop = this._hoverWindowTop, HoverWindowWidth = this._hoverWindowWidth, HoverWindowHeight = this._hoverWindowHeight });
        }

        /// <summary>Sets hover window size and persists config.</summary>
        /// <param name="width">Width in pixels, or null to clear.</param>
        /// <param name="height">Height in pixels, or null to clear.</param>
        internal void SetHoverWindowSize(int? width, int? height)
        {
            this._hoverWindowWidth = width;
            this._hoverWindowHeight = height;
            this.SaveConfig(new ConfigModel { CalendarUrl = this._calendarUrl, RefreshMinutes = this._refreshMinutes, ShowHoverWindow = this._showHoverWindow, IgnoreFreeOrFollowing = this._ignoreFreeOrFollowing, HoverWindowLeft = this._hoverWindowLeft, HoverWindowTop = this._hoverWindowTop, HoverWindowWidth = this._hoverWindowWidth, HoverWindowHeight = this._hoverWindowHeight });
        }

        /// <summary>Gets whether free/following meetings are ignored.</summary>
        /// <returns>True if such meetings are ignored; otherwise false.</returns>
        internal bool GetIgnoreFreeOrFollowingForUi() => this._ignoreFreeOrFollowing;

        /// <summary>Sets whether free/following meetings are ignored and persists config.</summary>
        /// <param name="v">New value indicating whether to ignore free/following meetings.</param>
        internal void SetIgnoreFreeOrFollowing(bool v)
        {
            this._ignoreFreeOrFollowing = v;
            this.SaveConfig(new ConfigModel { CalendarUrl = this._calendarUrl, RefreshMinutes = this._refreshMinutes, ShowHoverWindow = this._showHoverWindow, IgnoreFreeOrFollowing = this._ignoreFreeOrFollowing, HoverWindowLeft = this._hoverWindowLeft, HoverWindowTop = this._hoverWindowTop, HoverWindowWidth = this._hoverWindowWidth, HoverWindowHeight = this._hoverWindowHeight });
        }

        /// <summary>
        /// Persists configuration to disk, updating the active calendar URL and refresh interval.
        /// </summary>
        /// <param name="config">Configuration model to save.</param>
        internal void SaveConfig(ConfigModel config)
        {
            ObjectDisposedException.ThrowIf(this._disposed, nameof(TrayApplication));
            try
            {
                // Update in-memory values prior to save.
                if (config.CalendarUrl is not null)
                {
                    this._calendarUrl = config.CalendarUrl;
                }

                if (config.RefreshMinutes is int rm && rm > 0 && rm < 1440)
                {
                    this._refreshMinutes = rm;
                }

                string json = JsonSerializer.Serialize(config, JsonSerializerOptionsCache.IndentedOptions);
                File.WriteAllText(this._configPath, json);
            }
            catch (Exception ex)
            {
                this._configErrorDetected = true;
                throw new InvalidOperationException("Failed to save configuration.", ex);
            }
        }

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
                    return;
                }

                string json = File.ReadAllText(this._configPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                ConfigModel? cfg = JsonSerializer.Deserialize<ConfigModel>(json);
                if (cfg is not null)
                {
                    this._calendarUrl = cfg.CalendarUrl ?? string.Empty;
                    if (cfg.RefreshMinutes is int rm && rm > 0 && rm < 1440)
                    {
                        this._refreshMinutes = rm;
                    }

                    if (cfg.ShowHoverWindow is bool sh)
                    {
                        this._showHoverWindow = sh;
                    }

                    if (cfg.HoverWindowLeft is int hl)
                    {
                        this._hoverWindowLeft = hl;
                    }

                    if (cfg.HoverWindowTop is int ht)
                    {
                        this._hoverWindowTop = ht;
                    }

                    if (cfg.HoverWindowWidth is int ww)
                    {
                        this._hoverWindowWidth = ww;
                    }

                    if (cfg.HoverWindowHeight is int wh)
                    {
                        this._hoverWindowHeight = wh;
                    }
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
                        File.Delete(invalidPath);
                    }

                    File.Move(this._configPath, invalidPath);
                }
                catch (IOException)
                {
                }
            }
            catch (UnauthorizedAccessException)
            {
                this._configErrorDetected = true;
            }
        }

        // Removed legacy MaybeShowBalloon method.
    }
}
