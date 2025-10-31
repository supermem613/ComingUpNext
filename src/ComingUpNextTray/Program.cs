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
        private bool _isManualRefreshing;
        private readonly SemaphoreSlim _refreshGate = new(1, 1); // prevent overlapping refresh calls

        public TrayApplication()
        {
            _configPath = GetConfigPath();
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "ComingUpNext"
            };
            var ctxMenu = new ContextMenuStrip();
            ctxMenu.Items.Add("Open Meeting", null, (_, _) => OpenMeeting());
            // Manual refresh item (separate from timer) with disable logic while in progress.
            var refreshItem = new ToolStripMenuItem("Refresh");
            refreshItem.Click += async (_, _) => await ManualRefreshAsync(refreshItem);
            ctxMenu.Items.Add(refreshItem);
            ctxMenu.Items.Add("Set Calendar URL", null, (_, _) => PromptForUrl());
            ctxMenu.Items.Add("Set Refresh Minutes", null, (_, _) => PromptForRefreshMinutes());
            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("Exit", null, (_, _) => Exit());
            _notifyIcon.ContextMenuStrip = ctxMenu;
            _notifyIcon.DoubleClick += (_, _) => OpenMeeting();
            // Create timers with default values; config may adjust refresh interval after loading.
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 5 * 60 * 1000 }; // default 5 min, can be overridden by config
            _refreshTimer.Tick += async (_, _) => await RefreshAsync();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 60 * 1000 }; // 1 min
            _uiTimer.Tick += (_, _) => UpdateUi();
            _uiTimer.Start();

            // Load config (may update interval) then start refresh timer.
            LoadConfig();
            _refreshTimer.Start();
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
            // Skip if already refreshing (timer or manual). Use non-blocking attempt.
            if (!await _refreshGate.WaitAsync(0)) return;
            try
            {
                if (string.IsNullOrWhiteSpace(_calendarUrl))
                {
                    _entries = Array.Empty<CalendarEntry>();
                    _nextMeeting = null;
                    UpdateUi();
                    return;
                }
                var entries = await _calendarService.FetchAsync(_calendarUrl);
                _entries = entries;
                _nextMeeting = NextMeetingSelector.GetNextMeeting(entries, DateTime.Now);
                UpdateUi();
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        private async Task ManualRefreshAsync(ToolStripMenuItem? item)
        {
            if (_isManualRefreshing) return; // avoid double-click
            _isManualRefreshing = true;
            if (item != null) item.Enabled = false;
            try
            {
                await RefreshAsync();
            }
            finally
            {
                if (item != null) item.Enabled = true;
                _isManualRefreshing = false;
            }
        }

        private void PromptForRefreshMinutes()
        {
            using var form = new Form { Width = 300, Height = 140, Text = "Set Refresh Minutes" };
            var currentMins = _refreshTimer.Interval / 1000 / 60;
            var lbl = new Label { Left = 10, Top = 15, Width = 260, Text = "Refresh interval (minutes):" };
            var num = new NumericUpDown { Left = 10, Top = 40, Width = 80, Minimum = 1, Maximum = 1440, Value = currentMins }; // up to 1 day
            var btnOk = new Button { Text = "Save", Left = 110, Width = 80, Top = 70, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 200, Width = 80, Top = 70, DialogResult = DialogResult.Cancel };
            form.Controls.Add(lbl);
            form.Controls.Add(num);
            form.Controls.Add(btnOk);
            form.Controls.Add(btnCancel);
            form.AcceptButton = btnOk;
            form.CancelButton = btnCancel;
            if (form.ShowDialog() == DialogResult.OK)
            {
                var mins = (int)num.Value;
                _refreshTimer.Interval = mins * 60 * 1000;
                SaveConfig(); // persist new value
            }
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

        private void UpdateUi()
        {
            _nextMeeting = NextMeetingSelector.GetNextMeeting(_entries, DateTime.Now);
            var now = DateTime.Now;
            var tooltip = NextMeetingSelector.FormatTooltip(_nextMeeting, now);
            _notifyIcon.Text = TruncateTooltip(tooltip);
            UpdateIcon(now);
            MaybeShowBalloon();
        }

        private void UpdateIcon(DateTime now)
        {
            try
            {
                int minutes = 0;
                if (_nextMeeting != null)
                {
                    var delta = _nextMeeting.StartTime - now;
                    if (delta.TotalMinutes > 0 && delta.TotalMinutes < 1000) // cap large values
                        minutes = (int)Math.Round(delta.TotalMinutes);
                    else if (delta.TotalMinutes <= 0)
                        minutes = 0; // started or starting now
                }

                using var bmp = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var (bg, fg) = GetColorsForMinutes(minutes);
                    using var bgBrush = new SolidBrush(bg);
                    g.FillRectangle(bgBrush, new Rectangle(0, 0, 32, 32));

                    string text = FormatMinutesForIcon(minutes);
                    int baseSize = text.Length switch
                    {
                        1 => 18,
                        2 => 16,
                        3 => 14,
                        _ => 12
                    };
                    using var font = new Font(FontFamily.GenericSansSerif, baseSize, FontStyle.Bold);
                    var size = g.MeasureString(text, font);
                    using var fgBrush = new SolidBrush(fg);
                    g.DrawString(text, font, fgBrush, (32 - size.Width) / 2f, (32 - size.Height) / 2f - 2);
                }

                // Dispose previous icon to avoid handle leak
                var oldIcon = _notifyIcon.Icon;
                _notifyIcon.Icon = Icon.FromHandle(bmp.GetHicon());
                oldIcon?.Dispose();
            }
            catch
            {
                // fallback silently
            }
        }

        // Internal for test visibility
        internal static string FormatMinutesForIcon(int minutes)
        {
            // Negative or zero -> show 0 (meeting started)
            if (minutes <= 0) return "0";
            if (minutes < 60) return minutes.ToString();
            if (minutes < 1440)
            {
                var hours = (int)Math.Round(minutes / 60.0);
                if (hours < 1) hours = 1;
                return hours + "h";
            }
            var days = (int)Math.Round(minutes / 1440.0);
            if (days < 1) days = 1;
            return days + "d";
        }

        internal static (Color background, Color foreground) GetColorsForMinutes(int minutes)
        {
            // thresholds: <5 red, <15 yellow, otherwise green. When 0 (started), use dark gray background & white text.
            if (minutes <= 0) return (Color.DarkGray, Color.White);
            if (minutes < 5) return (Color.Red, Color.White);
            if (minutes < 15) return (Color.Gold, Color.Black);
            return (Color.Green, Color.White);
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
                    if (doc?.RefreshMinutes is int mins && mins > 0 && mins < 24 * 60)
                    {
                        // Update timer interval if different from current (allow only after timers exist)
                        if (_refreshTimer != null)
                        {
                            _refreshTimer.Interval = mins * 60 * 1000;
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                // Preserve existing refresh minutes if file exists
                int? refreshMinutes = null;
                try
                {
                    if (File.Exists(_configPath))
                    {
                        var existing = JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(_configPath));
                        if (existing?.RefreshMinutes is int m && m > 0) refreshMinutes = m;
                    }
                }
                catch { }
                var model = new ConfigModel { CalendarUrl = _calendarUrl, RefreshMinutes = refreshMinutes ?? (_refreshTimer.Interval / 1000 / 60) };
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
        public int? RefreshMinutes { get; set; }
    }

    // Public wrapper for tests so they can verify color thresholds without instantiating TrayApplication.
    public static (Color background, Color foreground) GetColorsForMinutesForTest(int minutes) => TrayApplication.GetColorsForMinutes(minutes);
}