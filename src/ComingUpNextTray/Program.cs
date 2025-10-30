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
            _uiTimer.Tick += (_, _) => UpdateUi();
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
                UpdateUi();
                return;
            }
            var entries = await _calendarService.FetchAsync(_calendarUrl);
            _entries = entries;
            _nextMeeting = NextMeetingSelector.GetNextMeeting(entries, DateTime.Now);
            UpdateUi();
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

                    string text = minutes.ToString();
                    if (minutes >= 100) text = "99+"; // avoid overflow
                    using var font = new Font(FontFamily.GenericSansSerif, minutes >= 100 ? 10 : (minutes >= 10 ? 16 : 18), FontStyle.Bold);
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

    // Public wrapper for tests so they can verify color thresholds without instantiating TrayApplication.
    public static (Color background, Color foreground) GetColorsForMinutesForTest(int minutes) => TrayApplication.GetColorsForMinutes(minutes);
}