namespace ComingUpNextTray.Services
{
    using System.Globalization;
    using System.Text;
    using ComingUpNextTray.Models;

    /// <summary>
    /// Service responsible for downloading and parsing ICS calendar data.
    /// </summary>
    internal sealed class CalendarService : IDisposable
    {
        // HttpClient is intended to be reused; dispose only when this service is disposed (CA1001).
        private readonly HttpClient httpClient = new ();
        private bool disposed;

        /// <summary>
        /// Fetches and parses calendar entries from the specified ICS URL.
        /// </summary>
        /// <param name="calendarIcsUrl">The calendar ICS URL string.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of parsed calendar entries.</returns>
        public async Task<IReadOnlyList<CalendarEntry>> FetchAsync(string calendarIcsUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(calendarIcsUrl))
            {
                return Array.Empty<CalendarEntry>();
            }

            if (Uri.TryCreate(calendarIcsUrl, UriKind.Absolute, out Uri? uri))
            {
                return await this.FetchAsync(uri, ct).ConfigureAwait(false);
            }

            return Array.Empty<CalendarEntry>();
        }

        /// <summary>
        /// Fetches and parses calendar entries from the specified ICS <paramref name="calendarIcsUri"/>.
        /// </summary>
        /// <param name="calendarIcsUri">Absolute ICS calendar feed URI.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of parsed calendar entries.</returns>
        public async Task<IReadOnlyList<CalendarEntry>> FetchAsync(Uri calendarIcsUri, CancellationToken ct = default)
        {
            try
            {
                using HttpResponseMessage resp = await this.httpClient.GetAsync(calendarIcsUri, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    return Array.Empty<CalendarEntry>();
                }

                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                string text = Encoding.UTF8.GetString(bytes);
                return ParseIcs(text);
            }
            catch (HttpRequestException)
            {
                return Array.Empty<CalendarEntry>();
            }
            catch (TaskCanceledException)
            {
                return Array.Empty<CalendarEntry>();
            }
        }

        /// <summary>
        /// Disposes the underlying <see cref="HttpClient"/> instance used by this service.
        /// </summary>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.httpClient.Dispose();
        }

        /// <summary>
        /// Parses an ICS text payload into calendar entries. Handles line unfolding, VEVENT boundaries and DTSTART/DTEND/DESCRIPTION/URL fields.
        /// </summary>
        /// <param name="ics">Raw ICS content.</param>
        /// <returns>Ordered list of parsed entries (may be empty).</returns>
        internal static IReadOnlyList<CalendarEntry> ParseIcs(string ics)
        {
            List<CalendarEntry> list = new ();
            if (string.IsNullOrWhiteSpace(ics))
            {
                return list;
            }

            // ICS lines can be folded: continuation lines start with space or tab. Unfold first.
            StringBuilder unfolded = new ();
            using (StringReader reader = new (ics))
            {
                string? line;
                string? prev = null;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith(' ') || line.StartsWith('\t'))
                    {
                        prev += line.TrimStart();
                    }
                    else
                    {
                        if (prev != null)
                        {
                            unfolded.AppendLine(prev);
                        }

                        prev = line;
                    }
                }

                if (prev != null)
                {
                    unfolded.AppendLine(prev);
                }
            }

            CalendarEntry? current = null;
            foreach (string raw in unfolded.ToString().Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    current = new CalendarEntry { Title = "(No Title)", StartTime = DateTime.MinValue, EndTime = DateTime.MinValue };
                }
                else if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null && current.StartTime != DateTime.MinValue)
                    {
                        if (current.EndTime == DateTime.MinValue || current.EndTime <= current.StartTime)
                        {
                            current.EndTime = current.StartTime.AddHours(1);
                        }

                        list.Add(current);
                    }

                    current = null;
                }
                else if (current != null)
                {
                    int colonIdx = line.IndexOf(':', StringComparison.Ordinal);
                    if (colonIdx < 0)
                    {
                        continue;
                    }

                    string keyPart = line.Substring(0, colonIdx);
                    string value = line[(colonIdx + 1) ..].Trim();

                    // parameters separated by ; in keyPart
                    string key = keyPart.Split(';')[0].ToUpperInvariant();
                    switch (key)
                    {
                        case "SUMMARY":
                            current.Title = value;
                            break;
                        case "DTSTART":
                            DateTime start = ParseDate(value);
                            if (start != DateTime.MinValue)
                            {
                                current.StartTime = start;
                            }

                            break;
                        case "DTEND":
                            DateTime end = ParseDate(value);
                            if (end != DateTime.MinValue)
                            {
                                current.EndTime = end;
                            }

                            break;
                        case "URL":
                            TryAssignUrl(current, value);
                            break;
                        case "DESCRIPTION":
                            if (current.MeetingUrl == null)
                            {
                                int idx = value.IndexOf("http", StringComparison.OrdinalIgnoreCase);
                                if (idx >= 0)
                                {
                                    string? segment = value[idx..].Split('\n', ' ', '\r', '\t').FirstOrDefault();
                                    if (!string.IsNullOrWhiteSpace(segment))
                                    {
                                        TryAssignUrl(current, segment.Trim());
                                    }
                                }
                            }

                            break;
                    }
                }
            }

            return list.OrderBy(e => e.StartTime).ToList();
        }

        /// <summary>
        /// Parses a DATE or DATE-TIME field from ICS data into a local <see cref="DateTime"/>. Supports UTC (Z suffix), local date-time and date-only formats.
        /// </summary>
        /// <param name="val">Raw ICS value (e.g. 20240122T130000Z).</param>
        /// <returns>The parsed local DateTime or <see cref="DateTime.MinValue"/> when invalid.</returns>
        internal static DateTime ParseDate(string val)
        {
            try
            {
                if (val.EndsWith('Z'))
                {
                    // UTC time
                    string core = val.TrimEnd('Z');
                    if (DateTime.TryParseExact(core, "yyyyMMdd'T'HHmmss", null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime utc))
                    {
                        return utc.ToLocalTime();
                    }
                }

                if (val.Length == 8 && DateTime.TryParseExact(val, "yyyyMMdd", null, DateTimeStyles.None, out DateTime dateOnly))
                {
                    // treat all-day as local midnight
                    return dateOnly;
                }

                if (DateTime.TryParseExact(val, "yyyyMMdd'T'HHmmss", null, DateTimeStyles.AssumeLocal, out DateTime local))
                {
                    return local;
                }
            }
            catch (FormatException)
            {
                // ignored - return MinValue below.
            }
            catch (ArgumentException)
            {
            }

            return DateTime.MinValue;
        }

        private static void TryAssignUrl(CalendarEntry entry, string candidate)
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
            {
                entry.MeetingUrl = uri;
            }
        }
    }
}
