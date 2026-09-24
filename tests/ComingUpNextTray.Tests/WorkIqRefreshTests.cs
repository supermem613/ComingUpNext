using System.Globalization;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ComingUpNextTray;
using ComingUpNextTray.Models;
using ComingUpNextTray.Services;
using Xunit;

namespace ComingUpNextTray.Tests
{
    public class WorkIqRefreshTests
    {
        [Fact]
        public async Task MissingSelectedExecutableKeepsWorkIqSelectedAndReportsActionableError()
        {
            string configPath = Path.Combine(Path.GetTempPath(), "cun_workiq_" + Guid.NewGuid() + ".json");
            string calendarUrl = "https://127.0.0.1:1/calendar.ics";
            string missingExecutable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "workiq.exe");
            const string expectedError = "Work IQ executable was not found at the selected path. Clear the path to search PATH or choose another executable.";

            try
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(new ConfigModel
                {
                    CalendarUrl = calendarUrl,
                    CalendarSource = CalendarSourceKind.WorkIq,
                    WorkIqExecutablePath = missingExecutable,
                    WorkIqAccount = "marcusm@microsoft.com",
                }));

                using TrayApplication app = new TrayApplication(configPath);
                bool refreshed = await app.RefreshAsync();
                app.SaveCurrentConfig();

                using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(configPath));
                JsonElement root = saved.RootElement;

                Assert.Equal(
                    (false, expectedError, "WorkIq", calendarUrl, TrayApplication.IconState.NoMeeting),
                    (
                        refreshed,
                        app.GetLastFetchErrorForUi(),
                        ReadString(root, "CalendarSource"),
                        ReadString(root, "CalendarUrl"),
                        app.ComputeIconState(DateTime.Now)));
            }
            finally
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
        }

        [Fact]
        public async Task WorkIqStructuredEventFlowsIntoExistingSelector()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "cun_workiq_" + Guid.NewGuid().ToString("N"));
            string configPath = Path.Combine(tempDirectory, "config.json");
            string argsPath = Path.Combine(tempDirectory, "args.txt");
            const string accountEmail = "marcusm@microsoft.com";
            string cancelledStartTime = DateTimeOffset.UtcNow.AddMinutes(5).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            string cancelledEndTime = DateTimeOffset.UtcNow.AddMinutes(10).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            string startTime = DateTimeOffset.UtcNow.AddMinutes(15).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            string endTime = DateTimeOffset.UtcNow.AddMinutes(45).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            string secondStartTime = DateTimeOffset.UtcNow.AddMinutes(60).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            string secondEndTime = DateTimeOffset.UtcNow.AddMinutes(90).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            string expectedStartLocal = DateTime.SpecifyKind(
                DateTime.ParseExact(startTime, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeKind.Utc).ToLocalTime().ToString("O", CultureInfo.InvariantCulture);
            string expectedSecondStartLocal = DateTime.SpecifyKind(
                DateTime.ParseExact(secondStartTime, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeKind.Utc).ToLocalTime().ToString("O", CultureInfo.InvariantCulture);

            const string serverScript = """
                $ErrorActionPreference = 'Stop'
                $callCountPath = Join-Path $PSScriptRoot 'call-count.txt'
                $callNumber = 1
                if (Test-Path $callCountPath) {
                    $callNumber = [int][System.IO.File]::ReadAllText($callCountPath) + 1
                }
                [System.IO.File]::WriteAllText($callCountPath, [string]$callNumber)
                [System.IO.File]::AppendAllText((Join-Path $PSScriptRoot 'args.txt'), [string]::Join('|', $args) + [Environment]::NewLine)
                $urlIndex = [Array]::IndexOf($args, '-u')
                $accountIndex = [Array]::IndexOf($args, '--account')
                $entityUrl = if ($urlIndex -ge 0 -and $urlIndex + 1 -lt $args.Length) { $args[$urlIndex + 1] } else { '' }
                $account = if ($accountIndex -ge 0 -and $accountIndex + 1 -lt $args.Length) { $args[$accountIndex + 1] } else { '' }
                if ($callNumber -eq 1) {
                    $cancelledEvent = @{
                        iCalUId = 'cancelled-workiq-event'
                        id = 'cancelled-graph-event'
                        subject = 'Cancelled Work IQ meeting'
                        start = @{ dateTime = '__CANCELLED_START_TIME__'; timeZone = 'UTC' }
                        end = @{ dateTime = '__CANCELLED_END_TIME__'; timeZone = 'UTC' }
                        isCancelled = $true
                        showAs = 'busy'
                    }
                    $event = @{
                        iCalUId = 'workiq-event-1'
                        id = 'graph-event-1'
                        subject = 'Work IQ meeting'
                        start = @{ dateTime = '__START_TIME__'; timeZone = 'UTC' }
                        end = @{ dateTime = '__END_TIME__'; timeZone = 'UTC' }
                        isCancelled = $false
                        showAs = 'busy'
                        onlineMeeting = @{ joinUrl = 'https://teams.microsoft.com/l/meetup-join/123' }
                    }
                    $response = @{
                        '@odata.context' = 'https://graph.microsoft.com/v1.0/$metadata#users/events'
                        value = @($cancelledEvent, $event)
                        '@odata.nextLink' = 'https://graph.microsoft.com/v1.0/me/calendarView?$skiptoken=next'
                    }
                } else {
                    $event = @{
                        iCalUId = 'workiq-event-2'
                        id = 'graph-event-2'
                        subject = 'Second page meeting'
                        start = @{ dateTime = '__SECOND_START_TIME__'; timeZone = 'UTC' }
                        end = @{ dateTime = '__SECOND_END_TIME__'; timeZone = 'UTC' }
                        isCancelled = $false
                        showAs = 'busy'
                    }
                    $response = @{ value = @($event) }
                }
                [Console]::Out.WriteLine(($response | ConvertTo-Json -Compress -Depth 20))
                """;

            try
            {
                Directory.CreateDirectory(tempDirectory);
                File.WriteAllText(Path.Combine(tempDirectory, "fake-workiq.ps1"), serverScript
                    .Replace("__CANCELLED_START_TIME__", cancelledStartTime, StringComparison.Ordinal)
                    .Replace("__CANCELLED_END_TIME__", cancelledEndTime, StringComparison.Ordinal)
                    .Replace("__START_TIME__", startTime, StringComparison.Ordinal)
                    .Replace("__END_TIME__", endTime, StringComparison.Ordinal)
                    .Replace("__SECOND_START_TIME__", secondStartTime, StringComparison.Ordinal)
                    .Replace("__SECOND_END_TIME__", secondEndTime, StringComparison.Ordinal));
                File.WriteAllText(
                    Path.Combine(tempDirectory, "workiq.cmd"),
                    "@echo off\r\n\"%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"%~dp0fake-workiq.ps1\" %*\r\n");

                File.WriteAllText(configPath, JsonSerializer.Serialize(new ConfigModel
                {
                    CalendarUrl = "https://127.0.0.1:1/calendar.ics",
                    CalendarSource = CalendarSourceKind.WorkIq,
                    WorkIqExecutablePath = Path.Combine(tempDirectory, "workiq.cmd"),
                    WorkIqAccount = accountEmail,
                }));

                using TrayApplication app = new TrayApplication(configPath);
                bool refreshed = await app.RefreshAsync();
                CalendarEntry? next = app.GetNextMeetingForUi();
                CalendarEntry? second = app.GetSecondMeetingForUi();
                string[][] calls = File.ReadAllLines(argsPath)
                    .Select(line => line.Split('|'))
                    .ToArray();

                Assert.Equal(
                    (true, "Work IQ meeting", expectedStartLocal, "workiq-event-1", "https://teams.microsoft.com/l/meetup-join/123", "Second page meeting", expectedSecondStartLocal, "workiq-event-2", accountEmail, accountEmail, 2, true, "/me/calendarView?$skiptoken=next", (string?)null),
                    (
                        refreshed,
                        next?.Title,
                        next?.StartTime.ToString("O", CultureInfo.InvariantCulture),
                        next?.Uid,
                        next?.MeetingUrl?.ToString(),
                        second?.Title,
                        second?.StartTime.ToString("O", CultureInfo.InvariantCulture),
                        second?.Uid,
                        calls.Length > 0 && calls[0].Length > 2 ? calls[0][2] : string.Empty,
                        calls.Length > 1 && calls[1].Length > 2 ? calls[1][2] : string.Empty,
                        calls.Length,
                        calls.Length > 0 && calls[0].Length > 4 && HasTwoDayWindow(calls[0][4]),
                        calls.Length > 1 && calls[1].Length > 4 ? Uri.UnescapeDataString(calls[1][4]) : string.Empty,
                        app.GetLastFetchErrorForUi()));
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public async Task CancellingWorkIqRefreshStopsTheChildProcess()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "cun_workiq_" + Guid.NewGuid().ToString("N"));
            string configPath = Path.Combine(tempDirectory, "config.json");
            string processIdPath = Path.Combine(tempDirectory, "child.pid");
            const string accountEmail = "marcusm@microsoft.com";
            const string serverScript = """
                $ErrorActionPreference = 'Stop'
                [System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'child.pid'), [string]$PID)
                while ($true) {
                    Start-Sleep -Seconds 1
                }
                """;

            try
            {
                Directory.CreateDirectory(tempDirectory);
                File.WriteAllText(Path.Combine(tempDirectory, "fake-workiq.ps1"), serverScript);
                File.WriteAllText(
                    Path.Combine(tempDirectory, "workiq.cmd"),
                    "@echo off\r\n\"%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"%~dp0fake-workiq.ps1\" %*\r\n");
                File.WriteAllText(configPath, JsonSerializer.Serialize(new ConfigModel
                {
                    CalendarSource = CalendarSourceKind.WorkIq,
                    WorkIqExecutablePath = Path.Combine(tempDirectory, "workiq.cmd"),
                    WorkIqAccount = accountEmail,
                }));

                using TrayApplication app = new TrayApplication(configPath);
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                Task<bool> refresh = app.RefreshAsync(cancellation.Token);
                int childProcessId = await WaitForProcessIdAsync(processIdPath);
                cancellation.Cancel();
                bool refreshed = await refresh;

                Assert.Equal((false, false), (refreshed, IsProcessRunning(childProcessId)));
            }
            finally
            {
                if (File.Exists(processIdPath) && int.TryParse(File.ReadAllText(processIdPath), out int processId))
                {
                    TryTerminateProcessTree(processId);
                }

                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public async Task TimedOutWorkIqFetchStopsTheChildProcessAndReportsActionableError()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "cun_workiq_" + Guid.NewGuid().ToString("N"));
            string processIdPath = Path.Combine(tempDirectory, "child.pid");
            const string accountEmail = "marcusm@microsoft.com";
            const string serverScript = """
                $ErrorActionPreference = 'Stop'
                [System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'child.pid'), [string]$PID)
                while ($true) {
                    Start-Sleep -Seconds 1
                }
                """;

            try
            {
                Directory.CreateDirectory(tempDirectory);
                File.WriteAllText(Path.Combine(tempDirectory, "fake-workiq.ps1"), serverScript);
                string executablePath = Path.Combine(tempDirectory, "workiq.cmd");
                File.WriteAllText(
                    executablePath,
                    "@echo off\r\n\"%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"%~dp0fake-workiq.ps1\" %*\r\n");

                WorkIqCalendarService service = new WorkIqCalendarService(TimeSpan.FromSeconds(2));
                Task<IReadOnlyList<CalendarEntry>> fetch = service.FetchAsync(accountEmail, executablePath, CancellationToken.None);
                int childProcessId = await WaitForProcessIdAsync(processIdPath);
                WorkIqException exception = await Assert.ThrowsAsync<WorkIqException>(() => fetch);

                Assert.Equal(
                    ("Work IQ fetch did not finish within 2 seconds. Check Work IQ, then retry.", false),
                    (exception.Message, IsProcessRunning(childProcessId)));
            }
            finally
            {
                if (File.Exists(processIdPath) && int.TryParse(File.ReadAllText(processIdPath), out int processId))
                {
                    TryTerminateProcessTree(processId);
                }

                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        private static bool HasTwoDayWindow(string entityUrl)
        {
            int queryStart = entityUrl.IndexOf('?', StringComparison.Ordinal);
            if (queryStart < 0)
            {
                return false;
            }

            string decodedQuery = Uri.UnescapeDataString(entityUrl[(queryStart + 1)..]);
            string? startValue = ReadQueryValue(decodedQuery, "startDateTime");
            string? endValue = ReadQueryValue(decodedQuery, "endDateTime");

            return entityUrl.StartsWith("/me/calendarView?", StringComparison.Ordinal) &&
                startValue is not null &&
                endValue is not null &&
                DateTimeOffset.TryParse(startValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset start) &&
                DateTimeOffset.TryParse(endValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset end) &&
                end - start == TimeSpan.FromDays(2) &&
                decodedQuery.Contains("&$select=", StringComparison.Ordinal);
        }

        private static string? ReadQueryValue(string query, string name)
        {
            foreach (string parameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = parameter.IndexOf('=', StringComparison.Ordinal);
                if (separator > 0 && string.Equals(parameter[..separator], name, StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(parameter[(separator + 1)..]);
                }
            }

            return null;
        }

        private static async Task<int> WaitForProcessIdAsync(string path)
        {
            // The fake process writes its PID in under one second; allow ten seconds for slow test-host startup.
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (File.Exists(path) && int.TryParse(File.ReadAllText(path), out int processId))
                {
                    return processId;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            throw new TimeoutException("The fake Work IQ process did not start.");
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void TryTerminateProcessTree(int processId)
        {
            try
            {
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
