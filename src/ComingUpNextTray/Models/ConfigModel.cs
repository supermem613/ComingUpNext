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

        /// <summary>Gets or sets the config schema version.</summary>
        public int Version { get; set; } = CurrentVersion;
    }
}
