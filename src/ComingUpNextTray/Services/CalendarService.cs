using ComingUpNextTray.Models;
using System.Text;

namespace ComingUpNextTray.Services;

public sealed class CalendarService
{
    private readonly HttpClient _httpClient = new();

    public async Task<IReadOnlyList<CalendarEntry>> FetchAsync(string calendarIcsUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(calendarIcsUrl))
            return Array.Empty<CalendarEntry>();
        try
        {
            using var resp = await _httpClient.GetAsync(calendarIcsUrl, ct);
            if (!resp.IsSuccessStatusCode)
                return Array.Empty<CalendarEntry>();
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var text = Encoding.UTF8.GetString(bytes);
            return ParseIcs(text);
        }
        catch
        {
            return Array.Empty<CalendarEntry>();
        }
    }

    internal static IReadOnlyList<CalendarEntry> ParseIcs(string ics)
    {
        var list = new List<CalendarEntry>();
        if (string.IsNullOrWhiteSpace(ics)) return list;
        // ICS lines can be folded: continuation lines start with space or tab. Unfold first.
        var unfolded = new StringBuilder();
        using (var reader = new StringReader(ics))
        {
            string? line;
            string? prev = null;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(" ") || line.StartsWith("\t"))
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
            if (prev != null) unfolded.AppendLine(prev);
        }

        CalendarEntry? current = null;
        foreach (var raw in unfolded.ToString().Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new CalendarEntry { Title = "(No Title)", StartTime = DateTime.MinValue, EndTime = DateTime.MinValue };
            }
            else if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null && current.StartTime != DateTime.MinValue)
                {
                    if (current.EndTime == DateTime.MinValue || current.EndTime <= current.StartTime)
                        current.EndTime = current.StartTime.AddHours(1);
                    list.Add(current);
                }
                current = null;
            }
            else if (current != null)
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;
                var keyPart = line.Substring(0, colonIdx);
                var value = line.Substring(colonIdx + 1).Trim();
                // parameters separated by ; in keyPart
                var key = keyPart.Split(';')[0].ToUpperInvariant();
                switch (key)
                {
                    case "SUMMARY":
                        current.Title = value;
                        break;
                    case "DTSTART":
                        var start = ParseDate(value);
                        if (start != DateTime.MinValue)
                            current.StartTime = start;
                        break;
                    case "DTEND":
                        var end = ParseDate(value);
                        if (end != DateTime.MinValue)
                            current.EndTime = end;
                        break;
                    case "URL":
                        current.MeetingUrl = value;
                        break;
                    case "DESCRIPTION":
                        if (string.IsNullOrWhiteSpace(current.MeetingUrl))
                        {
                            var idx = value.IndexOf("http", StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                var segment = value.Substring(idx).Split('\n',' ','\r','\t').FirstOrDefault();
                                if (!string.IsNullOrWhiteSpace(segment))
                                    current.MeetingUrl = segment.Trim();
                            }
                        }
                        break;
                }
            }
        }
        return list.OrderBy(e => e.StartTime).ToList();
    }

    internal static DateTime ParseDate(string val)
    {
        // Handle formats: YYYYMMDD, YYYYMMDDTHHMMSSZ, YYYYMMDDTHHMMSS (floating)
        try
        {
            if (val.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            {
                // UTC time
                var core = val.TrimEnd('Z');
                if (DateTime.TryParseExact(core, "yyyyMMdd'T'HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var utc))
                    return utc.ToLocalTime();
            }
            if (val.Length == 8 && DateTime.TryParseExact(val, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var dateOnly))
            {
                // treat all-day as local midnight
                return dateOnly;
            }
            if (DateTime.TryParseExact(val, "yyyyMMdd'T'HHmmss", null, System.Globalization.DateTimeStyles.AssumeLocal, out var local))
                return local;
        }
        catch { }
        return DateTime.MinValue;
    }
}
