using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using ComingUpNextTray.Services;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: NextMeetingInspector <http(s)-url-to-ics> [ignoreFreeOrFollowing:true|false]");
            return 2;
        }

        string url = args[0];

        bool ignoreFreeOrFollowing = true;
        if (args.Length > 1 && bool.TryParse(args[1], out bool parsed))
        {
            ignoreFreeOrFollowing = parsed;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            Console.Error.WriteLine("Invalid URL - must be an absolute http(s) URL.");
            return 2;
        }

        try
        {
            // Create a delegating handler that injects a User-Agent header to mimic a browser.
            using var delegating = new UserAgentHandler("Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
            {
                InnerHandler = new System.Net.Http.HttpClientHandler()
            };

            // CalendarService has an internal constructor accepting a HttpMessageHandler; our tool has InternalsVisibleTo so we can call it.
            using var svc = new CalendarService(delegating);
            IReadOnlyList<ComingUpNextTray.Models.CalendarEntry> entries = await svc.FetchWithErrorsAsync(uri).ConfigureAwait(false);

            Console.WriteLine($"Fetched entries: {entries.Count}");

            var next = NextMeetingSelector.GetNextMeeting(entries, DateTime.Now, ignoreFreeOrFollowing);
            if (next is null)
            {
                Console.WriteLine("No upcoming meetings found by selector.");
                return 0;
            }

            Console.WriteLine($"Next meeting: {next.Title}");
            Console.WriteLine($"Start: {next.StartTime:u}");
            Console.WriteLine($"End:   {next.EndTime:u}");
            Console.WriteLine($"Url:   {next.MeetingUrl}");
            Console.WriteLine($"Free/Following: {next.IsFreeOrFollowing}");
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Fetch error: {ex.Message}");
            return 3;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation canceled.");
            return 4;
        }
    }
}

/// <summary>
/// Delegating handler that ensures a User-Agent header is present on outgoing requests.
/// </summary>
sealed class UserAgentHandler : DelegatingHandler
{
    private readonly string userAgent;

    public UserAgentHandler(string userAgent)
    {
        this.userAgent = userAgent ?? string.Empty;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        if (request.Headers.UserAgent.Count == 0)
        {
            request.Headers.UserAgent.ParseAdd(this.userAgent);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
