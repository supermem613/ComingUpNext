namespace ComingUpNextTray.Models
{
    /// <summary>
    /// Serializable configuration persisted to JSON. Versioned for future migrations.
    /// </summary>
    internal sealed class ConfigModel
    {
        /// <summary>Current configuration file schema version.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Gets or sets the calendar URL (ICS feed).</summary>
        public string? CalendarUrl { get; set; }

        /// <summary>Gets or sets the refresh interval in minutes.</summary>
        public int? RefreshMinutes { get; set; }

        /// <summary>Gets or sets a value indicating whether free or "following" meetings should be ignored.</summary>
        public bool? IgnoreFreeOrFollowing { get; set; } = true;

        /// <summary>Gets or sets the config schema version.</summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary>Gets or sets a value indicating whether the hover window is shown.</summary>
        public bool ShowHoverWindow { get; set; } = true;

        /// <summary>Gets or sets saved left coordinate of hover window (screen coordinates).</summary>
        public int? HoverWindowLeft { get; set; }

        /// <summary>Gets or sets saved top coordinate of hover window (screen coordinates).</summary>
        public int? HoverWindowTop { get; set; }

        /// <summary>Gets or sets saved width of hover window in pixels.</summary>
        public int? HoverWindowWidth { get; set; }

        /// <summary>Gets or sets saved height of hover window in pixels.</summary>
        public int? HoverWindowHeight { get; set; }

        /// <summary>Gets or sets maximum hover title width in pixels for truncation (optional).</summary>
        public int? MaxHoverTitleWidth { get; set; }

        /// <summary>Gets or sets maximum menu text width in pixels for truncation (optional).</summary>
        public int? MaxMenuTextWidth { get; set; }
    }
}
