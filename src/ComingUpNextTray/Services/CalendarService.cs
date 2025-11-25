namespace ComingUpNextTray.Services
{
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using ComingUpNextTray.Models;

    /// <summary>
    /// Service responsible for downloading and parsing ICS calendar data.
    /// </summary>
    internal sealed class CalendarService : IDisposable
    {
        private static readonly char[] LineSplitSeparators = new[] { '\r', '\n' };

        // HttpClient is intended to be reused; dispose only when this service is disposed (CA1001).
        private readonly HttpClient httpClient;

        // Lightweight change validators; we intentionally do NOT cache ICS content or parsed entries.
        private string? lastEtag;
        private DateTimeOffset? lastModified;

        // Fallback when servers do not provide ETag/Last-Modified: store a hash of the last response body
        private string? lastContentHash;
        private bool disposed;

        /// <summary>Initializes a new instance of the <see cref="CalendarService"/> class.</summary>
        internal CalendarService()
        {
            this.httpClient = new HttpClient();
        }

        /// <summary>Initializes a new instance of the <see cref="CalendarService"/> class using a custom <see cref="HttpMessageHandler"/>. Internal for test injection; production code uses the default constructor.</summary>
        /// <param name="handler">Custom HTTP message handler.</param>
        internal CalendarService(HttpMessageHandler handler)
        {
            this.httpClient = new HttpClient(handler, disposeHandler: true);
        }

        /// <summary>Gets a value indicating whether previously observed change validators (ETag or Last-Modified) are available.</summary>
        public bool HasChangeValidators => this.lastEtag != null || this.lastModified != null;

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
        /// Performs a conditional GET using previously observed ETag / Last-Modified validators (if any) and
        /// returns parsed entries only when the server indicates the resource changed. On 304 Not Modified an empty list is returned
        /// allowing callers to decide whether to reuse prior results. This service purposely does not retain prior entries or ICS text.
        /// </summary>
        /// <param name="calendarIcsUri">ICS feed URI.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of parsed entries when changed; empty list on 304 or errors.</returns>
        public async Task<IReadOnlyList<CalendarEntry>> FetchIfChangedAsync(Uri calendarIcsUri, CancellationToken ct = default)
        {
            try
            {
                using HttpRequestMessage req = new (HttpMethod.Get, calendarIcsUri);
                if (!string.IsNullOrEmpty(this.lastEtag))
                {
                    req.Headers.TryAddWithoutValidation("If-None-Match", this.lastEtag);
                }

                if (this.lastModified.HasValue)
                {
                    req.Headers.IfModifiedSince = this.lastModified;
                }

                using HttpResponseMessage resp = await this.httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    return Array.Empty<CalendarEntry>();
                }

                if (!resp.IsSuccessStatusCode)
                {
                    return Array.Empty<CalendarEntry>();
                }

                // Read the body so we can compute a fallback hash when the server doesn't provide validators.
                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                // Compute content hash
                string newHash = ComputeHash(bytes);

                // If server does not provide ETag or Last-Modified, use the hash fallback to detect unchanged content.
                bool hasEtag = resp.Headers.ETag != null;
                bool hasLastModified = resp.Content.Headers.LastModified.HasValue;
                if (!hasEtag && !hasLastModified)
                {
                    if (!string.IsNullOrEmpty(this.lastContentHash) && this.lastContentHash == newHash)
                    {
                        // Content unchanged compared to last observed body; avoid parsing.
                        return Array.Empty<CalendarEntry>();
                    }

                    // Update stored body hash so future requests can be compared.
                    this.lastContentHash = newHash;
                }

                // Update any validators the server did provide (may be null)
                this.lastEtag = resp.Headers.ETag?.ToString();
                this.lastModified = resp.Content.Headers.LastModified;
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
        /// Similar to <see cref="FetchAsync(Uri, CancellationToken)"/> but propagates failures as exceptions
        /// so callers can inspect error messages. Use this when the caller wants to display errors to the user.
        /// </summary>
        /// <param name="calendarIcsUri">Absolute ICS calendar feed URI.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of parsed calendar entries.</returns>
        /// <exception cref="HttpRequestException">Thrown when an HTTP error occurs (non-success status or network failure).</exception>
        public async Task<IReadOnlyList<CalendarEntry>> FetchWithErrorsAsync(Uri calendarIcsUri, CancellationToken ct = default)
        {
            using HttpResponseMessage resp = await this.httpClient.GetAsync(calendarIcsUri, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                // Build a more helpful message for UI display.
                int code = (int)resp.StatusCode;
                string reason = resp.ReasonPhrase ?? resp.StatusCode.ToString();
                string loc = resp.Headers.Location?.ToString() ?? string.Empty;
                string hint = string.Empty;
                if (code >= 300 && code < 400)
                {
                    hint = " Redirected location may require authentication.";
                }
                else if (code == 401 || code == 403)
                {
                    hint = " Calendar feed appears to require authentication (401/403).";
                }

                string msg = !string.IsNullOrEmpty(loc)
                    ? $"HTTP {code} {reason} -> {loc}.{hint}"
                    : $"HTTP {code} {reason}.{hint}";

                throw new HttpRequestException(msg);
            }

            byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            string text = Encoding.UTF8.GetString(bytes);
            return ParseIcs(text);
        }

        /// <summary>
        /// Same as <see cref="FetchWithErrorsAsync"/> but uses conditional validators to potentially avoid downloading the body.
        /// Throws only when the server returns an error status for a changed resource; 304 yields an empty list.
        /// </summary>
        /// <param name="calendarIcsUri">ICS feed URI.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Parsed entries when changed; empty list when not modified.</returns>
        /// <exception cref="HttpRequestException">If HTTP status is non-success (excluding 304).</exception>
        public async Task<IReadOnlyList<CalendarEntry>> FetchIfChangedWithErrorsAsync(Uri calendarIcsUri, CancellationToken ct = default)
        {
            using HttpRequestMessage req = new (HttpMethod.Get, calendarIcsUri);
            if (!string.IsNullOrEmpty(this.lastEtag))
            {
                req.Headers.TryAddWithoutValidation("If-None-Match", this.lastEtag);
            }

            if (this.lastModified.HasValue)
            {
                req.Headers.IfModifiedSince = this.lastModified;
            }

            using HttpResponseMessage resp = await this.httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                return Array.Empty<CalendarEntry>();
            }

            if (!resp.IsSuccessStatusCode)
            {
                int code = (int)resp.StatusCode;
                string reason = resp.ReasonPhrase ?? resp.StatusCode.ToString();
                string loc = resp.Headers.Location?.ToString() ?? string.Empty;
                string hint = string.Empty;
                if (code >= 300 && code < 400)
                {
                    hint = " Redirected location may require authentication.";
                }
                else if (code == 401 || code == 403)
                {
                    hint = " Calendar feed appears to require authentication (401/403).";
                }

                string msg = !string.IsNullOrEmpty(loc)
                    ? $"HTTP {code} {reason} -> {loc}.{hint}"
                    : $"HTTP {code} {reason}.{hint}";

                throw new HttpRequestException(msg);
            }

            this.lastEtag = resp.Headers.ETag?.ToString();
            this.lastModified = resp.Content.Headers.LastModified;
            string text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseIcs(text);
        }

        /// <summary>
        /// Indicates whether conditional request validators were previously observed (ETag or Last-Modified).
        /// </summary>

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
        /// <param name="now">Optional reference time used for recurrence expansion; when omitted the current local time is used.</param>
        /// <returns>Ordered list of parsed entries (may be empty).</returns>
        internal static IReadOnlyList<CalendarEntry> ParseIcs(string ics, DateTime? now = null)
        {
            List<CalendarEntry> list = new ();
            if (string.IsNullOrWhiteSpace(ics))
            {
                return list;
            }

            // 1. Unfold lines (RFC5545 3.1) – continuation lines start with space or tab.
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

            // Temporary storage for recurrence handling
            CalendarEntry? current = null;
            string? currentRRule = null; // Raw RRULE line (after colon)
            List<(DateTime dt, string? tz)> currentExDates = new (); // EXDATE values

            DateTime nowLocal = now ?? DateTime.Now; // For recurrence expansion cut-off. Allow injecting 'now' for tests.

            foreach (string raw in unfolded.ToString().Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    current = new CalendarEntry { Title = "(No Title)", StartTime = DateTime.MinValue, EndTime = DateTime.MinValue };
                    currentRRule = null;
                    currentExDates.Clear();
                }
                else if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null && current.StartTime != DateTime.MinValue)
                    {
                        if (current.EndTime == DateTime.MinValue || current.EndTime <= current.StartTime)
                        {
                            current.EndTime = current.StartTime.AddHours(1);
                        }

                        // Add base occurrence if in future (or today) and not excluded.
                        if (!IsExcluded(current.StartTime, currentExDates))
                        {
                            list.Add(current);
                        }

                        // Expand simple weekly recurrences (RRULE FREQ=WEEKLY) to show upcoming meetings.
                        if (currentRRule != null)
                        {
                            foreach (CalendarEntry extra in ExpandRecurrence(current, currentRRule, currentExDates, nowLocal))
                            {
                                list.Add(extra);
                            }
                        }
                    }

                    current = null;
                    currentRRule = null;
                    currentExDates.Clear();
                }
                else if (current != null)
                {
                    int colonIdx = line.IndexOf(':', StringComparison.Ordinal);
                    if (colonIdx < 0)
                    {
                        continue; // malformed line
                    }

                    string keyPart = line.Substring(0, colonIdx);
                    string value = line[(colonIdx + 1) ..].Trim();

                    string[] keySegments = keyPart.Split(';');
                    string key = keySegments[0].ToUpperInvariant();
                    string? tzid = keySegments.Skip(1)
                        .Select(seg => seg.StartsWith("TZID=", StringComparison.OrdinalIgnoreCase) ? seg[5..] : null)
                        .FirstOrDefault(seg => seg != null);

                    switch (key)
                    {
                        case "SUMMARY":
                            current.Title = UnescapeIcsText(value);

                            // Mark simple summary markers
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                string sv = value.Trim();
                                if (string.Equals(sv, "Free", StringComparison.OrdinalIgnoreCase) || sv.Contains("following", StringComparison.OrdinalIgnoreCase))
                                {
                                    current.IsFreeOrFollowing = true;
                                }
                            }

                            break;
                        case "TRANSP":
                            // TRANSP:TRANSPARENT often indicates free time
                            if (string.Equals(value, "TRANSPARENT", StringComparison.OrdinalIgnoreCase))
                            {
                                current.IsFreeOrFollowing = true;
                            }

                            break;
                        case "STATUS":
                            // Some calendars use STATUS:FREE
                            if (string.Equals(value, "FREE", StringComparison.OrdinalIgnoreCase))
                            {
                                current.IsFreeOrFollowing = true;
                            }

                            break;
                        case "BUSYSTATUS":
                            // Exchange/Outlook may emit BUSYSTATUS or X-MICROSOFT-CDO-BUSYSTATUS with values like FREE, BUSY, TENTATIVE
                            if (string.Equals(value, "FREE", StringComparison.OrdinalIgnoreCase))
                            {
                                current.IsFreeOrFollowing = true;
                            }

                            break;
                        case "X-MICROSOFT-CDO-BUSYSTATUS":
                            // Some feeds use the vendor-prefixed property. Treat FREE as placeholder/free time.
                            if (string.Equals(value, "FREE", StringComparison.OrdinalIgnoreCase))
                            {
                                current.IsFreeOrFollowing = true;
                            }

                            break;
                        case "DTSTART":
                            DateTime start = ParseDate(value, tzid);
                            if (start != DateTime.MinValue)
                            {
                                current.StartTime = start;
                            }

                            break;
                        case "DTEND":
                            DateTime end = ParseDate(value, tzid);
                            if (end != DateTime.MinValue)
                            {
                                current.EndTime = end;
                            }

                            break;
                        case "EXDATE":
                            // Comma separated date-times; each may need TZ conversion.
                            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                DateTime ex = ParseDate(part, tzid);
                                if (ex != DateTime.MinValue)
                                {
                                    currentExDates.Add((ex, tzid));
                                }
                            }

                            break;
                        case "RRULE":
                            currentRRule = value; // We'll parse later.
                            break;
                        case "URL":
                            TryAssignUrl(current, value);
                            break;
                        case "ATTACH":
                            // Some calendars put join links in ATTACH fields.
                            TryAssignUrl(current, value);
                            break;
                        case "X-ALT-DESC":
                            // HTML formatted description may include anchors; fall through to DESCRIPTION handling below.
                            goto case "DESCRIPTION";
                        case "DESCRIPTION":
                            if (current.MeetingUrl == null)
                            {
                                // Try to extract href="..." or href='...' from HTML descriptions (X-ALT-DESC or DESCRIPTION with HTML).
                                int hrefIdx = -1;
                                string? href = null;

                                // Unescape DESCRIPTION text so any escaped commas/semicolons/newlines are normalized.
                                string desc = UnescapeIcsText(value);

                                // Look for href="..."
                                hrefIdx = desc.IndexOf("href=\"", StringComparison.OrdinalIgnoreCase);
                                if (hrefIdx >= 0)
                                {
                                    int hrefStart = hrefIdx + 6; // length of href="
                                    int hrefEnd = desc.IndexOf('"', hrefStart);
                                    if (hrefEnd > hrefStart)
                                    {
                                        href = desc[hrefStart..hrefEnd];
                                    }
                                }

                                // Look for href='...'
                                if (href is null)
                                {
                                    hrefIdx = desc.IndexOf("href='", StringComparison.OrdinalIgnoreCase);
                                    if (hrefIdx >= 0)
                                    {
                                        int hrefStart2 = hrefIdx + 6; // length of href='
                                        int hrefEnd2 = desc.IndexOf('\'', hrefStart2);
                                        if (hrefEnd2 > hrefStart2)
                                        {
                                            href = desc[hrefStart2..hrefEnd2];
                                        }
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(href))
                                {
                                    TryAssignUrl(current, href.Trim());
                                }
                                else
                                {
                                    // Fallback: simple http substring extraction (existing behavior).
                                    int idx = desc.IndexOf("http", StringComparison.OrdinalIgnoreCase);
                                    if (idx >= 0)
                                    {
                                        string? segment = desc[idx..].Split('\n', ' ', '\r', '\t').FirstOrDefault();
                                        if (!string.IsNullOrWhiteSpace(segment))
                                        {
                                            TryAssignUrl(current, segment.Trim());
                                        }
                                    }
                                }
                            }

                            break;
                    }
                }
            }

            // Return ordered, distinct by StartTime+Title (avoid duplicates if RRULE created original again)
            return list
                .OrderBy(e => e.StartTime)
                .GroupBy(e => (e.StartTime, e.Title))
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Inspect an ICS payload and return diagnostic information including raw VEVENTs, parsed entries and an expansion log.
        /// </summary>
        /// <param name="ics">Raw ICS content.</param>
        /// <param name="now">Optional 'now' reference for deterministic expansion.</param>
        /// <returns>Diagnostic inspection result.</returns>
        internal static Models.IcsInspectionResult InspectIcsDiagnostics(string ics, DateTime? now = null)
        {
            Models.IcsInspectionResult result = new Models.IcsInspectionResult();
            if (string.IsNullOrWhiteSpace(ics))
            {
                return result;
            }

            // Capture raw VEVENTs
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(ics, "BEGIN:VEVENT.*?END:VEVENT", System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                result.AddRawEvent(m.Value);
            }

            // Parse entries using existing parser but capture expansion logs by temporarily redirecting output
            // We'll duplicate a small portion of ParseIcs logic to intercept expansion steps.
            DateTime nowLocal = now ?? DateTime.Now;

            // Use ParseIcs to get base entries
            IReadOnlyList<Models.CalendarEntry> entries = ParseIcs(ics, nowLocal);
            foreach (Models.CalendarEntry e in entries)
            {
                result.AddEntry($"{e.StartTime:u} - {e.EndTime:u} : {e.Title}");
            }

            // For each VEVENT that has an RRULE, attempt to expand and log candidates
            // Simple extraction: find RRULE and DTSTART within VEVENT blocks
            foreach (string vevent in result.RawEvents)
            {
                string? summary = null;
                string? dtstart = null;
                string? rrule = null;
                string? exdate = null;

                string[] lines = vevent.Split(LineSplitSeparators, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.StartsWith("SUMMARY:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        summary = UnescapeIcsText(line.Substring(8).Trim());
                    }

                    if (line.StartsWith("DTSTART", System.StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = line.Split(':', 2);
                        if (parts.Length > 1)
                        {
                            dtstart = parts[1].Trim();
                        }
                    }

                    if (line.StartsWith("RRULE:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        rrule = line.Substring(6).Trim();
                    }

                    if (line.StartsWith("EXDATE", System.StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = line.Split(':', 2);
                        if (parts.Length > 1)
                        {
                            exdate = parts[1].Trim();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(rrule) && !string.IsNullOrEmpty(dtstart))
                {
                    result.AddLog($"VEVENT: {summary} DTSTART={dtstart} RRULE={rrule}");
                }
            }

            result.AddLog($"Total parsed entries: {result.Entries.Count}");
            return result;
        }

        /// <summary>
        /// Parses a DATE or DATE-TIME field from ICS data into a local <see cref="DateTime"/>. Supports UTC (Z suffix), local date-time and date-only formats.
        /// </summary>
        /// <param name="val">Raw ICS value (e.g. 20240122T130000Z).</param>
        /// <returns>The parsed local DateTime or <see cref="DateTime.MinValue"/> when invalid.</returns>
        internal static DateTime ParseDate(string val) => ParseDate(val, null);

        /// <summary>
        /// Parses a DATE or DATE-TIME field with optional TZID into local time.
        /// </summary>
        /// <param name="val">Raw ICS value (e.g. 20240122T130000Z).</param>
        /// <param name="tzid">Optional timezone identifier from TZID parameter.</param>
        /// <returns>Local DateTime or <see cref="DateTime.MinValue"/> on failure.</returns>
        internal static DateTime ParseDate(string val, string? tzid)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tzid))
                {
                    // Attempt timezone-aware parsing first (value expected as local wall clock time of tzid).
                    if (TryParseLocalDateTime(val, out DateTime naive))
                    {
                        DateTime unspecified = DateTime.SpecifyKind(naive, DateTimeKind.Unspecified);
                        try
                        {
                            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(tzid);
                            DateTime converted = TimeZoneInfo.ConvertTime(unspecified, tz, TimeZoneInfo.Local);
                            return converted;
                        }
                        catch (TimeZoneNotFoundException)
                        {
                            // Fall through to normal parsing.
                        }
                        catch (InvalidTimeZoneException)
                        {
                        }
                    }
                }

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

        private static bool TryParseLocalDateTime(string val, out DateTime dt)
        {
            if (val.Length == 15 && DateTime.TryParseExact(val, "yyyyMMdd'T'HHmmss", null, DateTimeStyles.None, out dt))
            {
                return true;
            }

            if (val.Length == 8 && DateTime.TryParseExact(val, "yyyyMMdd", null, DateTimeStyles.None, out dt))
            {
                return true;
            }

            dt = default;
            return false;
        }

        private static bool IsExcluded(DateTime candidateStart, List<(DateTime dt, string? tz)> exclusions)
        {
            // Compare on local time equality; EXDATE converted to local already.
            return exclusions.Any(e => e.dt == candidateStart);
        }

        private static IEnumerable<CalendarEntry> ExpandRecurrence(CalendarEntry prototype, string rrule, List<(DateTime dt, string? tz)> exdates, DateTime now)
        {
            // Support a limited subset: FREQ=WEEKLY;BYDAY=...;UNTIL=...;INTERVAL=n
            Dictionary<string, string> parts = rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => p.Split('=', 2))
                .Where(a => a.Length == 2)
                .ToDictionary(a => a[0].ToUpperInvariant(), a => a[1], StringComparer.OrdinalIgnoreCase);

            if (!parts.TryGetValue("FREQ", out string? freq) || !freq.Equals("WEEKLY", StringComparison.OrdinalIgnoreCase))
            {
                yield break; // Not supported.
            }

            int interval = 1;
            if (parts.TryGetValue("INTERVAL", out string? intervalStr) && int.TryParse(intervalStr, out int parsedInterval) && parsedInterval > 0)
            {
                interval = parsedInterval;
            }

            // Determine weekdays
            DayOfWeek[] byDays = Array.Empty<DayOfWeek>();
            if (parts.TryGetValue("BYDAY", out string? bydayVal))
            {
                byDays = bydayVal.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(MapDay)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToArray();
            }

            if (byDays.Length == 0)
            {
                byDays = new[] { prototype.StartTime.DayOfWeek }; // fallback to original day
            }

            DateTime startAnchor = prototype.StartTime; // first DTSTART already in local time.

            // UNTIL handling
            DateTime untilLimit = now.AddMonths(3); // default 3-month lookahead
            if (parts.TryGetValue("UNTIL", out string? untilRaw))
            {
                // Honor UNTIL even when it's in the past. UNTIL defines the last valid
                // recurrence date; if it's before 'now' no future recurrences should be
                // generated. Previously we only applied UNTIL if it was > now which
                // could allow generation of future meetings incorrectly.
                DateTime untilParsed = ParseDate(untilRaw);
                if (untilParsed != DateTime.MinValue)
                {
                    untilLimit = untilParsed.ToLocalTime();
                }
            }

            // Generate occurrences week by week.
            DateTime generationStart = startAnchor;
            if (generationStart < now)
            {
                // Align to the nearest recurrence period respecting INTERVAL (in weeks).
                // Calculate how many full 'interval' periods have elapsed between startAnchor and now,
                // then back one period to capture a possible occurrence on 'now'.
                double daysBetween = (now.Date - startAnchor.Date).TotalDays;
                int periodWeeks = 1 * interval; // weeks per period
                int periodsElapsed = (int)Math.Floor(daysBetween / (7.0 * interval));
                int alignedPeriods = Math.Max(0, periodsElapsed - 1);
                generationStart = startAnchor.AddDays(alignedPeriods * 7 * interval);
            }

            int safetyCounter = 0;

            // Hard cap to avoid runaway generation
            // Continue if generationStart is within a week of untilLimit to ensure we don't miss
            // occurrences earlier in the week (e.g., Tuesday when generationStart lands on Thursday)
            while (generationStart <= untilLimit.AddDays(6) && safetyCounter < 2000)
            {
                foreach (DayOfWeek targetDow in byDays)
                {
                    DateTime candidate = generationStart.Date;
                    int diff = targetDow - candidate.DayOfWeek;
                    if (diff < 0)
                    {
                        // Target day is earlier in the week - move to next week
                        diff += 7;
                    }

                    candidate = candidate.AddDays(diff);

                    candidate = candidate.Date + prototype.StartTime.TimeOfDay;
                    if (candidate <= prototype.StartTime)
                    {
                        // Skip original (already added) or earlier
                        continue;
                    }

                    if (candidate > untilLimit)
                    {
                        continue;
                    }

                    if (candidate < now)
                    {
                        continue; // past occurrence
                    }

                    if (IsExcluded(candidate, exdates))
                    {
                        continue;
                    }

                    yield return new CalendarEntry
                    {
                        Title = prototype.Title,
                        StartTime = candidate,
                        EndTime = candidate + (prototype.EndTime - prototype.StartTime),
                        MeetingUrl = prototype.MeetingUrl,
                        IsFreeOrFollowing = prototype.IsFreeOrFollowing,
                    };
                }

                generationStart = generationStart.AddDays(7 * interval);
                safetyCounter++;
            }
        }

        private static DayOfWeek? MapDay(string token)
        {
            return token.ToUpperInvariant() switch
            {
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday,
                _ => null,
            };
        }

        private static void TryAssignUrl(CalendarEntry entry, string candidate)
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
            {
                entry.MeetingUrl = uri;
            }
        }

        // Unescape ICS TEXT per RFC 5545 section 3.3.11: backslash escapes for COMMA, SEMICOLON, BACKSLASH and NEWLINE
        private static string UnescapeIcsText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            StringBuilder sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\\' && i + 1 < raw.Length)
                {
                    char next = raw[i + 1];
                    switch (next)
                    {
                        case 'n':
                        case 'N':
                            sb.Append('\n');
                            i++;
                            break;
                        case ',':
                            sb.Append(',');
                            i++;
                            break;
                        case ';':
                            sb.Append(';');
                            i++;
                            break;
                        case '\\':
                            sb.Append('\\');
                            i++;
                            break;
                        default:
                            // Unknown escape, keep both chars
                            sb.Append(c);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static string ComputeHash(byte[] data)
        {
            // Return a hex-encoded SHA256 of the content for quick comparisons.
            byte[] hash = SHA256.HashData(data);
            StringBuilder sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
