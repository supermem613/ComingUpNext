namespace ComingUpNextTray.Services
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using ComingUpNextTray.Models;

    /// <summary>Fetches and maps calendar events through the manually installed Work IQ CLI.</summary>
    internal sealed class WorkIqCalendarService
    {
        private const string CalendarViewPath = "/me/calendarView";
        private const string CalendarViewSelection = "id,iCalUId,subject,start,end,isCancelled,showAs,onlineMeeting,onlineMeetingUrl";

        // Three native probes took 4.775, 9.106, and 9.843 seconds; 30 seconds gives over 3x headroom above the slowest observed call.
        private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromSeconds(30);
        private readonly TimeSpan processTimeout;

        /// <summary>Initializes a new instance of the <see cref="WorkIqCalendarService"/> class.</summary>
        public WorkIqCalendarService()
            : this(DefaultProcessTimeout)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="WorkIqCalendarService"/> class with a process timeout.</summary>
        /// <param name="processTimeout">Maximum time to wait for one Work IQ CLI process.</param>
        internal WorkIqCalendarService(TimeSpan processTimeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(processTimeout, TimeSpan.Zero);
            this.processTimeout = processTimeout;
        }

        /// <summary>Fetches the configured account's upcoming events through Work IQ.</summary>
        /// <param name="accountEmail">The account email selected by the user.</param>
        /// <param name="executablePath">An optional explicit CLI executable path.</param>
        /// <param name="cancellationToken">Token used to cancel the child process.</param>
        /// <returns>Calendar entries mapped from the Graph events response.</returns>
        internal async Task<IReadOnlyList<CalendarEntry>> FetchAsync(string? accountEmail, string? executablePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(accountEmail))
            {
                throw new WorkIqException("Enter the Work IQ account email in settings.");
            }

            string executable = ResolveExecutable(executablePath);
            string entityUrl = BuildEventsUrl(DateTimeOffset.UtcNow);
            List<CalendarEntry> entries = new List<CalendarEntry>();
            HashSet<string> visitedUrls = new HashSet<string>(StringComparer.Ordinal);

            while (true)
            {
                if (!visitedUrls.Add(entityUrl))
                {
                    throw new JsonException("Work IQ returned a repeated Graph next link.");
                }

                string output = await this.RunFetchAsync(executable, accountEmail.Trim(), entityUrl, cancellationToken).ConfigureAwait(false);
                using JsonDocument document = JsonDocument.Parse(output);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("value", out JsonElement values) ||
                    values.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("Work IQ did not return a Graph events collection.");
                }

                foreach (JsonElement value in values.EnumerateArray())
                {
                    if (value.ValueKind == JsonValueKind.Object &&
                        value.TryGetProperty("isCancelled", out JsonElement cancelled) &&
                        cancelled.ValueKind == JsonValueKind.True)
                    {
                        continue;
                    }

                    entries.Add(MapEvent(value));
                }

                string? nextUrl = GetNextUrl(root);
                if (nextUrl is null)
                {
                    return entries;
                }

                entityUrl = nextUrl;
            }
        }

        private static string BuildEventsUrl(DateTimeOffset now)
        {
            DateTimeOffset start = now.ToUniversalTime();
            DateTimeOffset end = now.ToUniversalTime().AddDays(2);
            return CalendarViewPath +
                "?startDateTime=" + Uri.EscapeDataString(start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)) +
                "&endDateTime=" + Uri.EscapeDataString(end.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)) +
                "&$select=" + CalendarViewSelection;
        }

        private static string? GetNextUrl(JsonElement root)
        {
            if (!root.TryGetProperty("@odata.nextLink", out JsonElement nextLink) || nextLink.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (nextLink.ValueKind != JsonValueKind.String ||
                !Uri.TryCreate(nextLink.GetString(), UriKind.Absolute, out Uri? uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(uri.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(uri.AbsolutePath, "/v1.0/me/calendarView", StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException("Work IQ returned an invalid Graph events next link.");
            }

            return CalendarViewPath + uri.Query;
        }

        private static CalendarEntry MapEvent(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Work IQ returned an invalid calendar event.");
            }

            string uid = ReadString(value, "iCalUId") ?? ReadString(value, "id") ?? string.Empty;
            string title = ReadString(value, "subject") ?? string.Empty;
            DateTime start = ReadLocalTime(value, "start");
            DateTime end = ReadLocalTime(value, "end");
            bool isFree = string.Equals(ReadString(value, "showAs"), "free", StringComparison.OrdinalIgnoreCase);
            string? meetingUrlText = ReadNestedString(value, "onlineMeeting", "joinUrl") ?? ReadString(value, "onlineMeetingUrl");
            Uri? meetingUrl = null;
            if (!string.IsNullOrWhiteSpace(meetingUrlText))
            {
                if (!Uri.TryCreate(meetingUrlText, UriKind.Absolute, out meetingUrl))
                {
                    throw new JsonException("Work IQ returned an invalid meeting URL.");
                }
            }

            return new CalendarEntry
            {
                Uid = uid,
                Title = title,
                StartTime = start,
                EndTime = end,
                IsFreeOrFollowing = isFree,
                MeetingUrl = meetingUrl,
            };
        }

        private static DateTime ReadLocalTime(JsonElement eventValue, string propertyName)
        {
            if (!eventValue.TryGetProperty(propertyName, out JsonElement dateTimeZone) ||
                dateTimeZone.ValueKind != JsonValueKind.Object ||
                !dateTimeZone.TryGetProperty("dateTime", out JsonElement dateTimeElement) ||
                dateTimeElement.ValueKind != JsonValueKind.String ||
                !dateTimeZone.TryGetProperty("timeZone", out JsonElement timeZoneElement) ||
                timeZoneElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"Work IQ event is missing its {propertyName} date and time zone.");
            }

            string dateTimeText = dateTimeElement.GetString() ?? throw new JsonException($"Work IQ returned an empty {propertyName} date.");
            string timeZoneText = timeZoneElement.GetString() ?? throw new JsonException($"Work IQ returned an empty {propertyName} time zone.");
            if (!DateTime.TryParse(dateTimeText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime wallClockTime))
            {
                throw new JsonException($"Work IQ returned an invalid {propertyName} date.");
            }

            try
            {
                TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneText);
                DateTime unspecifiedTime = DateTime.SpecifyKind(wallClockTime, DateTimeKind.Unspecified);
                return TimeZoneInfo.ConvertTimeToUtc(unspecifiedTime, timeZone).ToLocalTime();
            }
            catch (TimeZoneNotFoundException ex)
            {
                throw new JsonException($"Work IQ returned an unsupported time zone for {propertyName}.", ex);
            }
            catch (InvalidTimeZoneException ex)
            {
                throw new JsonException($"Work IQ returned an invalid time zone for {propertyName}.", ex);
            }
        }

        private static string? ReadNestedString(JsonElement value, string parentName, string propertyName)
        {
            return value.TryGetProperty(parentName, out JsonElement parent) && parent.ValueKind == JsonValueKind.Object
                ? ReadString(parent, propertyName)
                : null;
        }

        private static string? ReadString(JsonElement value, string propertyName)
        {
            return value.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static string ResolveExecutable(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                if (File.Exists(configuredPath))
                {
                    return Path.GetFullPath(configuredPath);
                }

                throw new WorkIqException("Work IQ executable was not found at the selected path. Clear the path to search PATH or choose another executable.");
            }

            string[] searchDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Trim().Trim('"'))
                .Where(Directory.Exists)
                .ToArray();

            foreach (string directory in searchDirectories)
            {
                string executable = Path.Combine(directory, "workiq.exe");
                if (File.Exists(executable))
                {
                    return executable;
                }
            }

            string architectureDirectory = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.Arm64 => "win-arm64",
                _ => string.Empty,
            };

            if (architectureDirectory.Length > 0)
            {
                foreach (string directory in searchDirectories)
                {
                    string shim = Path.Combine(directory, "workiq.cmd");
                    if (File.Exists(shim))
                    {
                        string nativeExecutable = Path.Combine(directory, "node_modules", "@microsoft", "workiq", "bin", architectureDirectory, "workiq.exe");
                        if (File.Exists(nativeExecutable))
                        {
                            return nativeExecutable;
                        }
                    }
                }
            }

            bool commandShimFound = searchDirectories.Any(directory => File.Exists(Path.Combine(directory, "workiq.cmd")));
            if (commandShimFound)
            {
                throw new WorkIqException("Work IQ is on PATH, but its native executable was not found. Choose workiq.exe in settings or reinstall the native CLI.");
            }

            throw new WorkIqException("Work IQ executable was not found. Install Work IQ or choose its executable in settings.");
        }

        private static void TerminateProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
            catch (Win32Exception ex)
            {
                throw new WorkIqException("Work IQ could not be stopped after timeout or cancellation. Close it and retry.", ex);
            }
        }

        private static ProcessStartInfo CreateStartInfo(string executable, string accountEmail, string entityUrl)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (string.Equals(Path.GetExtension(executable), ".cmd", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(executable), ".bat", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
                string command = QuoteCommandArgument(executable) +
                    " fetch --account " + QuoteCommandArgument(accountEmail) +
                    " -u " + QuoteCommandArgument(entityUrl);
                startInfo.Arguments = "/d /s /c \"" + command + "\"";
            }
            else
            {
                startInfo.FileName = executable;
                startInfo.ArgumentList.Add("fetch");
                startInfo.ArgumentList.Add("--account");
                startInfo.ArgumentList.Add(accountEmail);
                startInfo.ArgumentList.Add("-u");
                startInfo.ArgumentList.Add(entityUrl);
            }

            return startInfo;
        }

        private static string QuoteCommandArgument(string argument)
        {
            return "\"" + argument.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        private async Task<string> RunFetchAsync(string executable, string accountEmail, string entityUrl, CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = CreateStartInfo(executable, accountEmail, entityUrl);
            using Process process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    throw new WorkIqException("Work IQ could not be started. Check the executable path in settings.");
                }
            }
            catch (Win32Exception ex)
            {
                throw new WorkIqException("Work IQ could not be started. Check the executable path in settings.", ex);
            }

            process.StandardInput.Close();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task errorDrainTask = process.StandardError.BaseStream.CopyToAsync(Stream.Null, CancellationToken.None);
            using CancellationTokenSource waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            waitCancellation.CancelAfter(this.processTimeout);

            try
            {
                await process.WaitForExitAsync(waitCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TerminateProcessTree(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAll(outputTask, errorDrainTask).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new WorkIqException(
                    $"Work IQ fetch did not finish within {this.processTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} seconds. Check Work IQ, then retry.");
            }

            await errorDrainTask.ConfigureAwait(false);
            string output = await outputTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new WorkIqException($"Work IQ fetch failed with exit code {process.ExitCode}. Check the account email, Work IQ access, and sign-in.");
            }

            if (output.Contains("End User License", StringComparison.OrdinalIgnoreCase) &&
                output.Contains("workiq accept-eula", StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkIqException("Work IQ requires EULA acceptance. Run 'workiq accept-eula', then retry.");
            }

            return output;
        }
    }
}
