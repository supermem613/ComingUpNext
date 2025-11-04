namespace ComingUpNextTray.Services
{
    using System;
    using ComingUpNextTray.Models;

    /// <summary>
    /// Public facade for diagnostic inspection of ICS payloads.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1515:Make types internal", Justification = "Public for CLI diagnostic tooling")]
    public static class CalendarDiagnostics
    {
        /// <summary>
        /// Inspect the provided ICS content and return diagnostics.
        /// </summary>
        /// <param name="ics">ICS content.</param>
        /// <param name="now">Optional reference time.</param>
        /// <returns>Inspection result.</returns>
        public static IcsInspectionResult Inspect(string ics, DateTime? now = null)
        {
            return CalendarService.InspectIcsDiagnostics(ics, now);
        }
    }
}
