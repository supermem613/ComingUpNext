using ComingUpNextTray.Services;
using ComingUpNextTray.Models;
using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ComingUpNextTray.Tests")] 

namespace ComingUpNextTray;

static class Program
{
    private const string AppFolderName = "ComingUpNext";
    private const string ConfigFileName = "config.json";

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var trayApp = new TrayApplication();
        Application.Run();
    }

    private sealed class TrayApplication : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly CalendarService _calendarService = new();
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly System.Windows.Forms.Timer _uiTimer;
        private IReadOnlyList<CalendarEntry> _entries = Array.Empty<CalendarEntry>();
        private CalendarEntry? _nextMeeting;
        private DateTime _lastPopupForMeeting = DateTime.MinValue;
        private string? _calendarUrl;
        private readonly string _configPath;
        private bool _disposed;

        public TrayApplication()
        {
            _configPath = GetConfigPath();
            LoadConfig();
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "ComingUpNext"
            };
            var ctxMenu = new ContextMenuStrip();
            ctxMenu.Items.Add("Open Meeting", null, (_, _) => OpenMeeting());
            ctxMenu.Items.Add("Refresh", null, async (_, _) => await RefreshAsync());
            ctxMenu.Items.Add("Set Calendar URL", null, (_, _) => PromptForUrl());
            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("Exit", null, (_, _) => Exit());
            _notifyIcon.ContextMenuStrip = ctxMenu;
            _notifyIcon.DoubleClick += (_, _) => OpenMeeting();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 5 * 60 * 1000 }; // 5 min
            _refreshTimer.Tick += async (_, _) => await RefreshAsync();
            _refreshTimer.Start();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 60 * 1000 }; // 1 min
            _uiTimer.Tick += (_, _) => UpdateTooltipAndPopup();
            _uiTimer.Start();

            // initial load
            _ = RefreshAsync();
        }

        private void PromptForUrl()
        {
            using var form = new Form { Width = 400, Height = 130, Text = "Set Calendar URL" };
            var tb = new TextBox { Left = 10, Top = 10, Width = 360, Text = _calendarUrl ?? string.Empty };
            var btnOk = new Button { Text = "Save", Left = 210, Width = 80, Top = 40, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 300, Width = 80, Top = 40, DialogResult = DialogResult.Cancel };
            form.Controls.Add(tb);
            form.Controls.Add(btnOk);
            form.Controls.Add(btnCancel);
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;
            if (form.ShowDialog() == DialogResult.OK)
            {
                _calendarUrl = tb.Text.Trim();
                SaveConfig();
                _ = RefreshAsync();
            }
        }

        private async Task RefreshAsync()
        {
            if (string.IsNullOrWhiteSpace(_calendarUrl))
            {
                _entries = Array.Empty<CalendarEntry>();
                _nextMeeting = null;
                UpdateTooltipAndPopup();
                return;
            }
            var entries = await _calendarService.FetchAsync(_calendarUrl);
            _entries = entries;
            _nextMeeting = NextMeetingSelector.GetNextMeeting(entries, DateTime.Now);
            UpdateTooltipAndPopup();
        }

        private void OpenMeeting()
        {
            if (_nextMeeting?.MeetingUrl is string url && !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void UpdateTooltipAndPopup()
        {
            _nextMeeting = NextMeetingSelector.GetNextMeeting(_entries, DateTime.Now);
            var tooltip = NextMeetingSelector.FormatTooltip(_nextMeeting, DateTime.Now);
            _notifyIcon.Text = TruncateTooltip(tooltip);
            MaybeShowBalloon();
        }

        private void MaybeShowBalloon()
        {
            if (_nextMeeting == null) return;
            var now = DateTime.Now;
            var delta = _nextMeeting.StartTime - now;
            if (delta.TotalMinutes <= 15 && delta.TotalMinutes > 0)
            {
                // avoid spamming: show once per meeting
                if (_lastPopupForMeeting != _nextMeeting.StartTime)
                {
                    _notifyIcon.BalloonTipTitle = "Meeting soon";
                    _notifyIcon.BalloonTipText = NextMeetingSelector.FormatTooltip(_nextMeeting, now);
                    _notifyIcon.ShowBalloonTip(5000);
                    _lastPopupForMeeting = _nextMeeting.StartTime;
                }
            }
        }

        private static string TruncateTooltip(string s) => s.Length > 63 ? s.Substring(0, 60) + "..." : s;

        private static string GetExecutableDirectory()
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            return Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;
        }

        private static string GetLegacyConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, AppFolderName);
            return Path.Combine(folder, ConfigFileName);
        }

        private static string GetConfigPath()
        {
            var exeDir = GetExecutableDirectory();
            return Path.Combine(exeDir, ConfigFileName);
        }

        private void LoadConfig()
        {
            try
            {
                // Migration: if new path missing but legacy exists, copy over
                var legacy = GetLegacyConfigPath();
                if (!File.Exists(_configPath) && File.Exists(legacy))
                {
                    try
                    {
                        File.Copy(legacy, _configPath, overwrite: true);
                    }
                    catch { }
                }

                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var doc = JsonSerializer.Deserialize<ConfigModel>(json);
                    _calendarUrl = doc?.CalendarUrl;
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var model = new ConfigModel { CalendarUrl = _calendarUrl };
                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        private void Exit()
        {
            Application.Exit();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _refreshTimer.Dispose();
            _uiTimer.Dispose();
        }
    }

    private sealed class ConfigModel
    {
        public string? CalendarUrl { get; set; }
    }
}