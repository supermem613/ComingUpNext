namespace ComingUpNextTray.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Holds diagnostic information produced when inspecting an ICS payload.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1515:Make types internal", Justification = "Public for CLI diagnostic tooling")]
    public sealed class IcsInspectionResult
    {
        private readonly List<string> rawEvents = new List<string>();
        private readonly List<string> entries = new List<string>();
        private readonly List<string> expansionLog = new List<string>();

        /// <summary>
        /// Gets the raw VEVENT blocks extracted from the ICS payload.
        /// </summary>
        public IReadOnlyList<string> RawEvents => this.rawEvents;

        /// <summary>
        /// Gets the parsed calendar entries as textual summaries (Start - End - Title).
        /// </summary>
        public IReadOnlyList<string> Entries => this.entries;

        /// <summary>
        /// Gets the textual log produced during recurrence expansion for diagnostic purposes.
        /// </summary>
        public IReadOnlyList<string> ExpansionLog => this.expansionLog;

        /// <summary>
        /// Adds a raw VEVENT block (internal helper).
        /// </summary>
        /// <param name="v">The VEVENT text.</param>
        internal void AddRawEvent(string v)
        {
            this.rawEvents.Add(v);
        }

        /// <summary>
        /// Adds a textual entry summary (internal helper).
        /// </summary>
        /// <param name="s">The entry summary string.</param>
        internal void AddEntry(string s)
        {
            this.entries.Add(s);
        }

        /// <summary>
        /// Adds a diagnostic expansion log line (internal helper).
        /// </summary>
        /// <param name="s">The log line.</param>
        internal void AddLog(string s)
        {
            this.expansionLog.Add(s);
        }
    }
}
