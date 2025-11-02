using ComingUpNextTray.Services;
using ComingUpNextTray.Models;
using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ComingUpNextTray.Tests")]

namespace ComingUpNextTray {
    internal static class Program {
        private const string AppFolderName = "ComingUpNext";
        private const string ConfigFileName = "config.json";

        [STAThread]
        private static void Main() {
            ApplicationConfiguration.Initialize();

            using TrayApplication trayApp = new TrayApplication();
            Application.Run();
        }

        internal sealed class TrayApplication : IDisposable {
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

            // Icon cache: key is tuple (background.ToArgb(), foreground.ToArgb(), text)
            private readonly Dictionary<(int bg, int fg, string text), Icon> _iconCache = new();

            public TrayApplication() {
                // Always store config under user's AppData (roaming) folder.
                string? overridePath = Environment.GetEnvironmentVariable("COMINGUPNEXT_TEST_CONFIG_PATH");
                if (!string.IsNullOrWhiteSpace(overridePath)) {
                    _configPath = overridePath!;
                }
                else {
                    _configPath = GetAppDataConfigPath();
                }
                _notifyIcon = new NotifyIcon {
                    Icon = SystemIcons.Application,
                    Visible = true,
                    Text = "ComingUpNext"
                };
                ContextMenuStrip ctxMenu = new ContextMenuStrip();
                ctxMenu.Items.Add("Open Meeting", null, (_, _) => OpenMeeting());
                ctxMenu.Items.Add("Open Config Folder", null, (_, _) => OpenConfigFolder());
                // Manual refresh item (separate from timer) with disable logic while in progress.
                ToolStripMenuItem refreshItem = new ToolStripMenuItem("Refresh");
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

            private void PromptForUrl() {
                using Form form = new Form { Width = 400, Height = 130, Text = "Set Calendar URL" };
                TextBox tb = new TextBox { Left = 10, Top = 10, Width = 360, Text = _calendarUrl ?? string.Empty };
                Button btnOk = new Button { Text = "Save", Left = 210, Width = 80, Top = 40, DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Cancel", Left = 300, Width = 80, Top = 40, DialogResult = DialogResult.Cancel };
                form.Controls.Add(tb);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;
                if (form.ShowDialog() == DialogResult.OK) {
                    _calendarUrl = tb.Text.Trim();
                    SaveConfig();
                    _ = RefreshAsync();
                }
            }

            private async Task RefreshAsync() {
                // Skip if already refreshing (timer or manual). Use non-blocking attempt.
                if (!await _refreshGate.WaitAsync(0)) {
                    return;
                }

                try {
                    if (string.IsNullOrWhiteSpace(_calendarUrl)) {
                        _entries = Array.Empty<CalendarEntry>();
                        _nextMeeting = null;
                        UpdateUi();
                        return;
                    }
                    IReadOnlyList<CalendarEntry> entries = await _calendarService.FetchAsync(_calendarUrl);
                    _entries = entries;
                    _nextMeeting = NextMeetingSelector.GetNextMeeting(entries, DateTime.Now);
                    UpdateUi();
                }
                finally {
                    _refreshGate.Release();
                }
            }

            private async Task ManualRefreshAsync(ToolStripMenuItem? item) {
                if (_isManualRefreshing) {
                    return; // avoid double-click
                }

                _isManualRefreshing = true;
                if (item != null) {
                    item.Enabled = false;
                }

                try {
                    await RefreshAsync();
                }
                finally {
                    if (item != null) {
                        item.Enabled = true;
                    }

                    _isManualRefreshing = false;
                }
            }

            private void PromptForRefreshMinutes() {
                using Form form = new Form { Width = 300, Height = 140, Text = "Set Refresh Minutes" };
                int currentMins = _refreshTimer.Interval / 1000 / 60;
                Label lbl = new Label { Left = 10, Top = 15, Width = 260, Text = "Refresh interval (minutes):" };
                NumericUpDown num = new NumericUpDown { Left = 10, Top = 40, Width = 80, Minimum = 1, Maximum = 1440, Value = currentMins }; // up to 1 day
                Button btnOk = new Button { Text = "Save", Left = 110, Width = 80, Top = 70, DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Cancel", Left = 200, Width = 80, Top = 70, DialogResult = DialogResult.Cancel };
                form.Controls.Add(lbl);
                form.Controls.Add(num);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;
                if (form.ShowDialog() == DialogResult.OK) {
                    int mins = (int)num.Value;
                    _refreshTimer.Interval = mins * 60 * 1000;
                    SaveConfig(); // persist new value
                }
            }

            private void OpenMeeting() {
                if (_nextMeeting?.MeetingUrl is string url && !string.IsNullOrWhiteSpace(url)) {
                    try {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }

            private void UpdateUi() {
                _nextMeeting = NextMeetingSelector.GetNextMeeting(_entries, DateTime.Now);
                DateTime now = DateTime.Now;
                IconState state = ComputeIconState(now);
                string tooltip = state switch {
                    IconState.NoCalendar => "No calendar URL configured",
                    _ => NextMeetingSelector.FormatTooltip(_nextMeeting, now)
                };
                if (state == IconState.DistantFuture) {
                    tooltip += " (>1 day)";
                }

                _notifyIcon.Text = TruncateTooltip(tooltip);
                UpdateIcon(now);
                MaybeShowBalloon();
            }

            // Internal helper for tests to obtain full tooltip (untruncated) with distant future hint applied.
            internal string BuildTooltipForTest(DateTime now) {
                IconState state = ComputeIconState(now);
                string tooltip = state switch {
                    IconState.NoCalendar => "No calendar URL configured",
                    _ => NextMeetingSelector.FormatTooltip(_nextMeeting, now)
                };
                if (state == IconState.DistantFuture) {
                    tooltip += " (>1 day)";
                }

                return tooltip;
            }

            private void UpdateIcon(DateTime now) {
                try {
                    IconState state = ComputeIconState(now);
                    int minutesValue = 0;
                    if (state == IconState.MinutesRemaining && _nextMeeting != null) {
                        TimeSpan delta = _nextMeeting.StartTime - now;
                        minutesValue = (int)Math.Round(delta.TotalMinutes);
                        if (minutesValue < 0) {
                            minutesValue = 0;
                        }
                    }

                    using Bitmap bmp = new Bitmap(32, 32);
                    using (Graphics g = Graphics.FromImage(bmp)) {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        (Color bg, Color fg, string text) = GetIconVisual(state, minutesValue);
                        using SolidBrush bgBrush = new SolidBrush(bg);
                        g.FillRectangle(bgBrush, new Rectangle(0, 0, 32, 32));

                        if (!string.IsNullOrEmpty(text)) {
                            int fontSize = 18;
                            SizeF size;
                            Font font;
                            while (true) {
                                font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold);
                                size = g.MeasureString(text, font);
                                if (size.Width <= 30 && size.Height <= 30) {
                                    break;
                                }
                                font.Dispose();
                                fontSize--;
                                if (fontSize < 10) {
                                    font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold);
                                    size = g.MeasureString(text, font);
                                    break;
                                }
                            }
                            using (font) {
                                using SolidBrush fgBrush = new SolidBrush(fg);
                                g.DrawString(text, font, fgBrush, (32 - size.Width) / 2f, (32 - size.Height) / 2f - 2);
                            }
                        }
                    }

                    // Cache icons by colors+text to reduce GDI churn
                    (Color kbg, Color kfg, string ktext) = GetIconVisual(state, minutesValue);
                    (int bg, int fg, string text) key = (kbg.ToArgb(), kfg.ToArgb(), ktext);
                    Icon icon;
                    if (_iconCache.TryGetValue(key, out Icon? cached)) {
                        icon = cached;
                    }
                    else {
                        icon = Icon.FromHandle(bmp.GetHicon());
                        _iconCache[key] = icon;
                    }
                    Icon? oldIcon = _notifyIcon.Icon;
                    _notifyIcon.Icon = icon;
                    oldIcon?.Dispose();
                }
                catch {
                    // fallback silently
                }
            }

            internal enum IconState {
                NoCalendar,
                NoMeeting,
                DistantFuture,
                MinutesRemaining,
                Started
            }

            internal IconState ComputeIconState(DateTime now) {
                if (string.IsNullOrWhiteSpace(_calendarUrl)) {
                    return IconState.NoCalendar;
                }
                if (_nextMeeting == null) {
                    return IconState.NoMeeting;
                }
                TimeSpan delta = _nextMeeting.StartTime - now;
                if (delta.TotalMinutes >= 24 * 60) {
                    return IconState.DistantFuture;
                }
                if (delta.TotalMinutes <= 0) {
                    return IconState.Started;
                }
                return IconState.MinutesRemaining;
            }

            private static (Color bg, Color fg, string text) GetIconVisual(IconState state, int minutesValue = 0) {
                return state switch {
                    IconState.NoCalendar => (Color.DarkGray, Color.White, string.Empty),
                    IconState.NoMeeting => (Color.Green, Color.White, string.Empty),
                    IconState.DistantFuture => (Color.Green, Color.White, "∞"),
                    IconState.Started => (Color.Red, Color.White, "0"),
                    IconState.MinutesRemaining => minutesValue < 5 ? (Color.Red, Color.White, FormatMinutesForIcon(minutesValue))
                        : minutesValue < 15 ? (Color.Gold, Color.Black, FormatMinutesForIcon(minutesValue))
                        : (Color.Green, Color.White, FormatMinutesForIcon(minutesValue)),
                    _ => (Color.Green, Color.White, string.Empty)
                };
            }

            // Internal for test visibility
            internal static string FormatMinutesForIcon(int minutes) {
                if (minutes <= 0) {
                    return "0"; // meeting started / starting
                }
                if (minutes < 60) {
                    return minutes.ToString();
                }
                int hours = (int)Math.Round(minutes / 60.0);
                if (hours < 1) {
                    hours = 1;
                }
                if (hours > 23) {
                    hours = 23; // defensive; though >=24h uses DistantFuture icon.
                }
                return hours + "h"; // restore suffix for clarity
            }

            // Removed old GetColors method; visuals determined in GetIconVisual.

            private void MaybeShowBalloon() {
                if (_nextMeeting == null) {
                    return;
                }
                DateTime now = DateTime.Now;
                TimeSpan delta = _nextMeeting.StartTime - now;
                if (delta.TotalMinutes <= 15 && delta.TotalMinutes > 0) {
                    // avoid spamming: show once per meeting
                    if (_lastPopupForMeeting != _nextMeeting.StartTime) {
                        _notifyIcon.BalloonTipTitle = "Meeting soon";
                        string tooltip = NextMeetingSelector.FormatTooltip(_nextMeeting, now);
                        if (delta.TotalMinutes >= 24 * 60) {
                            tooltip += " (>1 day)"; // hint (defensive)
                        }
                        _notifyIcon.BalloonTipText = tooltip;
                        _notifyIcon.ShowBalloonTip(5000);
                        _lastPopupForMeeting = _nextMeeting.StartTime;
                    }
                }
            }

            private static string TruncateTooltip(string s) => s.Length > 63 ? s.Substring(0, 60) + "..." : s;

            private static string GetExecutableDirectory() {
                string exe = Environment.ProcessPath ?? Application.ExecutablePath;
                return Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;
            }

            private static string GetAppDataConfigPath() {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, AppFolderName);
                return Path.Combine(folder, ConfigFileName);
            }

            private void LoadConfig() {
                try {
                    string? dir = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                    if (!File.Exists(_configPath)) {
                        return;
                    }
                    string json = File.ReadAllText(_configPath);
                    if (string.IsNullOrWhiteSpace(json)) {
                        return; // treat empty file as default
                    }
                    ConfigModel? doc = null;
                    try {
                        doc = JsonSerializer.Deserialize<ConfigModel>(json);
                    }
                    catch (JsonException) {
                        if (json.Trim() == "{}") {
                            return; // tolerate empty object
                        }
                        // Malformed JSON: notify user and bail; keep existing defaults. Optionally rename bad file.
                        try {
                            string badPath = _configPath + ".invalid";
                            File.Move(_configPath, badPath, overwrite: true);
                            if (File.Exists(_configPath) && File.Exists(badPath)) {
                                // If move didn't remove original (rare), delete it.
                                try { File.Delete(_configPath); } catch { }
                            }
                        }
                        catch { }
                        ShowBalloon("Config Error", "Config file malformed. Renamed to config.json.invalid.");
                        return;
                    }

                    _calendarUrl = doc?.CalendarUrl;
                    if (doc?.RefreshMinutes is int mins && mins > 0 && mins < 24 * 60) {
                        _refreshTimer.Interval = mins * 60 * 1000;
                    }
                    // Future: handle version-specific migrations.
                }
                catch { }
            }

            internal void SaveConfig() {
                try {
                    int? refreshMinutes = null;
                    try {
                        if (File.Exists(_configPath)) {
                            ConfigModel? existing = JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(_configPath));
                            if (existing?.RefreshMinutes is int m && m > 0) {
                                refreshMinutes = m;
                            }
                        }
                    }
                    catch { }
                    ConfigModel model = new ConfigModel { CalendarUrl = _calendarUrl, RefreshMinutes = refreshMinutes ?? (_refreshTimer.Interval / 1000 / 60), Version = ConfigModel.CurrentVersion };
                    string json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                    string? dir = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(_configPath, json);
                }
                catch { }
            }

            private void Exit() {
                Application.Exit();
            }

            public void Dispose() {
                if (_disposed) {
                    return;
                }
                _disposed = true;
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _refreshTimer.Dispose();
                _uiTimer.Dispose();
                foreach (Icon ico in _iconCache.Values) {
                    try { ico.Dispose(); } catch { }
                }
                _iconCache.Clear();
            }

            // Internal for tests to validate config storage location.
            internal string GetConfigFilePathForTest() => _configPath;

            private void OpenConfigFolder() {
                try {
                    string? dir = Path.GetDirectoryName(_configPath);
                    if (dir != null) {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true });
                    }
                }
                catch { }
            }

            private void ShowBalloon(string title, string message) {
                try {
                    _notifyIcon.BalloonTipTitle = title;
                    _notifyIcon.BalloonTipText = message;
                    _notifyIcon.ShowBalloonTip(5000);
                }
                catch { }
            }
        }

        // Public test helper wrappers (avoid fragile reflection in tests)
        public static string FormatMinutesForIconForTest(int minutes) => TrayApplication.FormatMinutesForIcon(minutes);
        public static string GetConfigFilePathForTest() {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, AppFolderName, ConfigFileName);
        }

        private sealed class ConfigModel {
            public string? CalendarUrl { get; set; }
            public int? RefreshMinutes { get; set; }
            public int Version { get; set; } = CurrentVersion;
            public const int CurrentVersion = 1;
        }

        // Public wrapper for tests so they can verify color thresholds without instantiating TrayApplication.
        public static (Color background, Color foreground) GetColorsForMinutesForTest(int minutes) => TrayApplication.FormatMinutesForIcon(minutes) == "0" ? (Color.Red, Color.White) : (minutes < 5 ? (Color.Red, Color.White) : minutes < 15 ? (Color.Gold, Color.Black) : (Color.Green, Color.White));
    }
}
