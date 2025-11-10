namespace ComingUpNextTray
{
    using System;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;
    using ComingUpNextTray.Models;
    using ComingUpNextTray.Services;

    /// <summary>
    /// WinForms <see cref="ApplicationContext"/> hosting the lifetime of the tray icon
    /// and underlying <see cref="TrayApplication"/> logic. Disposing the context exits the app.
    /// </summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly TrayApplication app; // underlying logic instance
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem nextMeetingDisplayItem; // shows formatted first meeting
        private readonly ToolStripMenuItem secondMeetingDisplayItem; // shows formatted second meeting
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly System.Windows.Forms.Timer overlayTimer; // updates icon/tooltip more frequently
        private ToolStripMenuItem? toggleHoverWindowItem;
        private ToolStripMenuItem? toggleIgnoreFreeFollowingItem;
        private HoverWindow? hoverWindow;
        private bool disposed;
        private Icon? baseIcon;
        private bool refreshInProgress;
        private IntPtr lastOverlayIconHandle = IntPtr.Zero;
        private int alertStage; // 0 none, 1 fifteen-minute alert shown, 2 five-minute alert shown
        private CalendarEntry? lastAlertMeeting; // track meeting for which alerts were issued

        /// <summary>
        /// Initializes a new instance of the <see cref="TrayContext"/> class.
        /// Creates the NotifyIcon, context menu, and backing application logic.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Using centralized UiText constants; localization pending.")]
        public TrayContext()
        {
            // Instantiate underlying logic first (may load config, etc.).
            this.app = new TrayApplication();

            this.menu = new ContextMenuStrip();

            // Dynamic meeting display items (inserted at top later):
            this.nextMeetingDisplayItem = new ToolStripMenuItem(string.Empty) { Enabled = false }; // will show formatted first meeting
            this.secondMeetingDisplayItem = new ToolStripMenuItem(string.Empty) { Enabled = false }; // will show formatted second meeting

            // Core items
            ToolStripMenuItem openMeetingItem = new ToolStripMenuItem(UiText.OpenMeeting, null, this.OnOpenMeetingClick) { Enabled = false };
            ToolStripMenuItem copyMeetingLinkItem = new ToolStripMenuItem(UiText.CopyMeetingLink, null, this.OnCopyMeetingLinkClick) { Enabled = false };
            ToolStripMenuItem openCalendarItem = new ToolStripMenuItem(UiText.OpenCalendarUrl, null, this.OnOpenCalendarUrlClick) { Enabled = false };
            ToolStripMenuItem copyCalendarItem = new ToolStripMenuItem(UiText.CopyCalendarUrl, null, this.OnCopyCalendarUrlClick) { Enabled = false };
            ToolStripMenuItem setCalendarUrlItem = new ToolStripMenuItem(UiText.SetCalendarUrl, null, this.OnSetCalendarUrlClick);
            ToolStripMenuItem openConfigFolderItem = new ToolStripMenuItem(UiText.OpenConfigFolder, null, this.OnOpenConfigFolderClick);
            ToolStripMenuItem openConfigFileItem = new ToolStripMenuItem(UiText.OpenConfigFile, null, this.OnOpenConfigFileClick);
            this.toggleHoverWindowItem = new ToolStripMenuItem(UiText.ToggleHoverWindow, null, this.OnToggleHoverWindowClick);
            this.toggleIgnoreFreeFollowingItem = new ToolStripMenuItem(UiText.ToggleIgnoreFreeOrFollowing, null, this.OnToggleIgnoreFreeFollowingClick);
            ToolStripMenuItem resetHoverPositionItem = new ToolStripMenuItem("Reset Hover Window Position", null, this.OnResetHoverWindowPositionClick);
            ToolStripMenuItem refreshItem = new ToolStripMenuItem(UiText.Refresh, null, this.OnManualRefreshClick);

            // Refresh interval submenu
            ToolStripMenuItem refreshIntervalRoot = new ToolStripMenuItem(UiText.SetRefreshMinutes);
            foreach (int m in new[] { 1, 5, 10, 15, 30, 60 })
            {
                ToolStripMenuItem opt = new ToolStripMenuItem(m + " min", null, this.OnRefreshIntervalClick) { Tag = m };
                refreshIntervalRoot.DropDownItems.Add(opt);
            }

            // Mark current interval.
            int currentInterval = this.app.GetRefreshMinutesForUi();
            foreach (ToolStripMenuItem opt in refreshIntervalRoot.DropDownItems.OfType<ToolStripMenuItem>())
            {
                if (opt.Tag is int minutes && minutes == currentInterval)
                {
                    opt.Checked = true;
                    break;
                }
            }

            // About item
            ToolStripMenuItem aboutItem = new ToolStripMenuItem(UiText.About, null, this.OnAboutClick);
            ToolStripMenuItem exitItem = new ToolStripMenuItem(UiText.Exit, null, this.OnExitClick);

            // Insert meeting display placeholders at top.
            this.menu.Items.Add(this.nextMeetingDisplayItem);
            this.menu.Items.Add(this.secondMeetingDisplayItem);
            this.menu.Items.Add(new ToolStripSeparator());
            this.menu.Items.Add(openMeetingItem);
            this.menu.Items.Add(copyMeetingLinkItem);
            this.menu.Items.Add(openCalendarItem);
            this.menu.Items.Add(copyCalendarItem);
            this.menu.Items.Add(setCalendarUrlItem);
            this.menu.Items.Add(new ToolStripSeparator());
            this.menu.Items.Add(openConfigFolderItem);
            this.menu.Items.Add(openConfigFileItem);
            this.menu.Items.Add(this.toggleHoverWindowItem);
            this.menu.Items.Add(this.toggleIgnoreFreeFollowingItem);
            this.menu.Items.Add(resetHoverPositionItem);
            this.menu.Items.Add(refreshItem);
            this.menu.Items.Add(refreshIntervalRoot);
            this.menu.Items.Add(new ToolStripSeparator());
            this.menu.Items.Add(aboutItem);
            this.menu.Items.Add(exitItem);

            // Load icon from application resources (declared in csproj).
            using Icon? loadedIcon = LoadAppIcon();
            if (loadedIcon is not null)
            {
                // Keep an independent clone so later overlay draws do not risk using a disposed handle.
                this.baseIcon = (Icon)loadedIcon.Clone();
            }
            else
            {
                // Fallback: use application icon embedded in assembly if available.
                try
                {
                    this.baseIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
            }

            this.notifyIcon = new NotifyIcon
            {
                Icon = this.baseIcon ?? SystemIcons.Application,
                Text = UiText.ApplicationTitle,
                Visible = true,
                ContextMenuStrip = this.menu,
            };

            this.notifyIcon.DoubleClick += this.OnNotifyIconDoubleClick;

            // Setup periodic refresh timer (UI thread) for network/calendar fetches.
            this.refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = Math.Max(1, this.app.GetRefreshMinutesForUi()) * 60_000,
            };

            // Keep continuation on UI thread to safely update NotifyIcon.
            this.refreshTimer.Tick += async (s, e) => await this.RefreshAndUpdateUiAsync().ConfigureAwait(true);

            // Fire initial async refresh without blocking constructor.
            _ = this.RefreshAndUpdateUiAsync();
            this.refreshTimer.Start();

            // Setup overlay update timer to refresh countdown visuals every 15 seconds.
            this.overlayTimer = new System.Windows.Forms.Timer
            {
                Interval = 15_000,
            };
            this.overlayTimer.Tick += (s, e) =>
            {
                if (!this.refreshInProgress)
                {
                    DateTime now = DateTime.Now;

                    // Advance meeting pointer if current finished without network refresh.
                    this.app.AdvanceMeetingIfEnded(now);
                    string tooltip = this.app.BuildTooltipForTest(now);
                    this.notifyIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
                    this.UpdateOverlayIcon(now);

                    // Threshold notifications.
                    TrayApplication.IconState state = this.app.ComputeIconState(now);
                    CalendarEntry? meeting = this.app.GetNextMeetingForUi();
                    if (meeting != null && meeting != this.lastAlertMeeting)
                    {
                        // New meeting; reset alert stage.
                        this.alertStage = 0;
                        this.lastAlertMeeting = meeting;
                    }

                    if (state == TrayApplication.IconState.MinutesRemaining && meeting is not null)
                    {
                        double minutes = (meeting.StartTime - now).TotalMinutes;
                        if (minutes <= 5 && this.alertStage < 2)
                        {
                            this.ShowBalloon(UiText.MeetingVerySoonBalloon);
                            this.alertStage = 2;
                        }
                        else if (minutes <= 15 && this.alertStage < 1)
                        {
                            this.ShowBalloon(UiText.MeetingSoonBalloon);
                            this.alertStage = 1;
                        }
                    }

                    // Keep hover window updated if visible and enabled
                    if (this.toggleHoverWindowItem?.Checked == true && this.hoverWindow is not null && !this.hoverWindow.IsDisposed)
                    {
                        string overlayTokenNow = this.app.GetOverlayText(now, includeUnit: true);

                        // Provide last fetch error if present so hover can prefer showing it.
                        string? fetchErr = this.app.GetLastFetchErrorForUi();
                        this.hoverWindow.UpdateMeeting(meeting, now, overlayTokenNow, fetchErr);

                        // update colors using central helper
                        double? minutes2 = meeting is not null ? (meeting.StartTime - now).TotalMinutes : null;
                        (Color bg2, Color fg2) = MeetingColorHelper.GetColors(state, minutes2);
                        this.hoverWindow.SetColors(bg2, fg2);
                    }
                }
            };
            this.overlayTimer.Start();

            // Show hover window at startup if enabled in config
            if (this.app.GetShowHoverWindowForUi())
            {
                this.toggleHoverWindowItem!.Checked = true;
                this.hoverWindow = new HoverWindow();
                this.hoverWindow.UpdateMeeting(this.app.GetNextMeetingForUi(), DateTime.Now, this.app.GetOverlayText(DateTime.Now, includeUnit: true), this.app.GetLastFetchErrorForUi());

                // Restore saved position if available, otherwise position near mouse
                int? savedLeft = this.app.GetHoverWindowLeftForUi();
                int? savedTop = this.app.GetHoverWindowTopForUi();
                if (savedLeft.HasValue && savedTop.HasValue)
                {
                    this.hoverWindow.Location = new Point(savedLeft.Value, savedTop.Value);
                }
                else
                {
                    Point p = Cursor.Position;
                    this.hoverWindow.Location = new Point(p.X + 16, p.Y - (this.hoverWindow.Height / 2));
                }

                // Persist position when moved
                this.hoverWindow.Move += this.OnHoverWindowMoved;

                this.hoverWindow.Show();
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.notifyIcon.Visible = false; // hide immediately
                this.notifyIcon.Dispose();
                this.nextMeetingDisplayItem.Dispose();
                this.secondMeetingDisplayItem.Dispose();
                this.toggleHoverWindowItem?.Dispose();
                this.toggleIgnoreFreeFollowingItem?.Dispose();
                this.menu.Dispose();
                if (this.hoverWindow is not null && !this.hoverWindow.IsDisposed)
                {
                    this.hoverWindow.Move -= this.OnHoverWindowMoved;
                    this.hoverWindow.Close();
                    this.hoverWindow.Dispose();
                }

                this.app.Dispose();
                this.refreshTimer.Stop();
                this.refreshTimer.Dispose();
                this.overlayTimer.Stop();
                this.overlayTimer.Dispose();
                this.baseIcon?.Dispose();
                this.DestroyLastOverlayHandle();
            }

            this.disposed = true;
            base.Dispose(disposing);
        }

        private static Icon? LoadAppIcon()
        {
            try
            {
                // The icon is included as Content: Resources\ComingUpNext.ico; attempt to load via file path.
                string? baseDir = AppContext.BaseDirectory;
                string iconPath = System.IO.Path.Combine(baseDir!, "Resources", "ComingUpNext.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    return new Icon(iconPath, 256, 256);
                }
            }
            catch (System.IO.IOException)
            {
                // Ignore and fall back.
            }
            catch (System.Security.SecurityException)
            {
                // Ignore and fall back.
            }

            return null;
        }

        private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
        {
            // For now show settings form when double-clicking.
            using SettingsForm form = new SettingsForm();
            form.Initialize(this.app);
            form.ShowDialog();
        }

        private void OnSettingsClick(object? sender, EventArgs e)
        {
            this.OnNotifyIconDoubleClick(sender, e);
        }

        private void OnSetCalendarUrlClick(object? sender, EventArgs e)
        {
            using SettingsForm form = new SettingsForm();
            form.Initialize(this.app);
            if (form.ShowDialog() == DialogResult.OK)
            {
                _ = this.RefreshAndUpdateUiAsync();
            }
        }

        private void OnExitClick(object? sender, EventArgs e)
        {
            this.ExitThread();
        }

        private async Task RefreshAndUpdateUiAsync()
        {
            if (this.refreshInProgress)
            {
                return;
            }

            try
            {
                this.refreshInProgress = true;
                bool ok = await this.app.RefreshAsync().ConfigureAwait(true); // stay on UI context for icon update
                DateTime now = DateTime.Now;
                string tooltip = this.app.BuildTooltipForTest(now);
                this.notifyIcon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip; // NotifyIcon.Text limit
                this.UpdateOverlayIcon(now);
                this.UpdateMenuState();

                // If hover window visible, update it immediately so users see refreshed info without waiting for overlay timer.
                if (this.toggleHoverWindowItem?.Checked == true && this.hoverWindow is not null && !this.hoverWindow.IsDisposed)
                {
                    DateTime nowLocal = DateTime.Now;
                    CalendarEntry? meeting = this.app.GetNextMeetingForUi();
                    string overlayTokenNow = this.app.GetOverlayText(nowLocal, includeUnit: true);
                    this.hoverWindow.UpdateMeeting(meeting, nowLocal, overlayTokenNow, this.app.GetLastFetchErrorForUi());
                    TrayApplication.IconState state = this.app.ComputeIconState(nowLocal);
                    double? minutes2 = meeting is not null ? (meeting.StartTime - nowLocal).TotalMinutes : null;
                    (Color bg2, Color fg2) = MeetingColorHelper.GetColors(state, minutes2);
                    this.hoverWindow.SetColors(bg2, fg2);
                }

                if (!ok)
                {
                    // Show a one-time balloon if calendar URL exists but fetch failed.
                    string url = this.app.GetCalendarUrlForUi();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        this.ShowErrorBalloon();
                    }
                }
            }
            finally
            {
                this.refreshInProgress = false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Using centralized UiText constants; localization pending.")]
        private void ShowErrorBalloon()
        {
            try
            {
                this.notifyIcon.BalloonTipTitle = UiText.ConfigErrorTitle;
                this.notifyIcon.BalloonTipText = UiText.ConfigErrorMessage;
                this.notifyIcon.ShowBalloonTip(3000);
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        private void UpdateMenuState()
        {
            CalendarEntry? next = this.app.GetNextMeetingForUi();
            CalendarEntry? second = this.app.GetSecondMeetingForUi();

            // Update display items text.
            // If a fetch error occurred, prefer showing the error message instead of "No upcoming meetings".
            string? fetchErr = this.app.GetLastFetchErrorForUi();
            if (!string.IsNullOrEmpty(fetchErr))
            {
                this.nextMeetingDisplayItem.Text = UiText.FetchErrorPrefix + fetchErr;
            }
            else
            {
                this.nextMeetingDisplayItem.Text = next is null ? UiText.NoUpcomingMeetings : NextMeetingSelector.FormatTooltip(next, DateTime.Now);
            }

            this.secondMeetingDisplayItem.Text = second is null ? string.Empty : NextMeetingSelector.FormatTooltip(second, DateTime.Now);
            this.secondMeetingDisplayItem.Visible = second is not null;
            string calendarUrl = this.app.GetCalendarUrlForUi();
            if (this.toggleHoverWindowItem is not null)
            {
                this.toggleHoverWindowItem.Checked = this.app.GetShowHoverWindowForUi();
            }

            if (this.toggleIgnoreFreeFollowingItem is not null)
            {
                this.toggleIgnoreFreeFollowingItem.Checked = this.app.GetIgnoreFreeOrFollowingForUi();
            }

            foreach (ToolStripMenuItem item in this.menu.Items.OfType<ToolStripMenuItem>())
            {
                switch (item.Text)
                {
                    case var s when s == UiText.OpenMeeting || s == UiText.CopyMeetingLink:
                        item.Enabled = next?.MeetingUrl != null;
                        break;
                    case var s when s == UiText.OpenCalendarUrl || s == UiText.CopyCalendarUrl:
                        item.Enabled = !string.IsNullOrWhiteSpace(calendarUrl);
                        break;
                }
            }
        }

        private void OnToggleIgnoreFreeFollowingClick(object? sender, EventArgs e)
        {
            if (this.toggleIgnoreFreeFollowingItem is null)
            {
                return;
            }

            bool newVal = !this.toggleIgnoreFreeFollowingItem.Checked;
            this.toggleIgnoreFreeFollowingItem.Checked = newVal;

            // Persist setting
            this.app.SetIgnoreFreeOrFollowing(newVal);

            // Refresh display to reflect possible changed next/second meeting
            _ = this.RefreshAndUpdateUiAsync();
        }

        private void OnToggleHoverWindowClick(object? sender, EventArgs e)
        {
            if (this.toggleHoverWindowItem is null)
            {
                return;
            }

            bool newVal = !this.toggleHoverWindowItem.Checked;
            this.toggleHoverWindowItem.Checked = newVal;

            // Persist setting
            this.app.SetShowHoverWindow(newVal);

            if (newVal)
            {
                // Show hover window immediately
                if (this.hoverWindow is null || this.hoverWindow.IsDisposed)
                {
                    this.hoverWindow = new HoverWindow();
                }

                this.hoverWindow.UpdateMeeting(this.app.GetNextMeetingForUi(), DateTime.Now, this.app.GetOverlayText(DateTime.Now), this.app.GetLastFetchErrorForUi());

                // choose colors consistent with overlay computation
                Color bg = Color.Black;
                Color fg = Color.White;
                TrayApplication.IconState state = this.app.ComputeIconState(DateTime.Now);
                if (state == TrayApplication.IconState.MinutesRemaining && this.app.GetNextMeetingForUi() is { } meeting)
                {
                    double minutes = (meeting.StartTime - DateTime.Now).TotalMinutes;
                    if (minutes <= 5)
                    {
                        bg = Color.Red;
                    }
                    else if (minutes <= 15)
                    {
                        bg = Color.Gold;
                        fg = Color.Black;
                    }
                    else
                    {
                        bg = Color.Green;
                    }
                }
                else if (state == TrayApplication.IconState.Started)
                {
                    bg = Color.DarkRed;
                }
                else if (state == TrayApplication.IconState.DistantFuture)
                {
                    bg = Color.MediumBlue;
                }
                else if (state == TrayApplication.IconState.NoMeeting)
                {
                    bg = Color.DimGray;
                }
                else if (state == TrayApplication.IconState.NoCalendar)
                {
                    bg = Color.DarkGray;
                }

                this.hoverWindow.SetColors(bg, fg);

                // Position near cursor as default
                Point p = Cursor.Position;
                this.hoverWindow.Location = new Point(p.X + 16, p.Y - (this.hoverWindow.Height / 2));
                this.hoverWindow.Show();
            }
            else
            {
                if (this.hoverWindow is not null && !this.hoverWindow.IsDisposed)
                {
                    this.hoverWindow.Move -= this.OnHoverWindowMoved;
                    this.hoverWindow.Close();
                    this.hoverWindow.Dispose();
                    this.hoverWindow = null;
                }
            }
        }

        private void OnResetHoverWindowPositionClick(object? sender, EventArgs e)
        {
            // Clear persisted position and size
            this.app.SetHoverWindowPosition(null, null);
            this.app.SetHoverWindowSize(null, null);

            // If hover window visible, move it to default near cursor and resize to default
            if (this.hoverWindow is not null && !this.hoverWindow.IsDisposed)
            {
                Point p = Cursor.Position;
                this.hoverWindow.Location = new Point(p.X + 16, p.Y - (this.hoverWindow.Height / 2));

                // reset size to default
                this.hoverWindow.Size = new Size(220, this.hoverWindow.Height);
            }
        }

        private void OnHoverWindowMoved(object? sender, EventArgs e)
        {
            if (this.hoverWindow is null || this.hoverWindow.IsDisposed)
            {
                return;
            }

            // Persist the top-left of the hover window
            this.app.SetHoverWindowPosition(this.hoverWindow.Location.X, this.hoverWindow.Location.Y);
        }

        private void UpdateOverlayIcon(DateTime now)
        {
            if (this.baseIcon is null)
            {
                return; // fallback icon only
            }

            string overlay = this.app.GetOverlayText(now);
            TrayApplication.IconState state = this.app.ComputeIconState(now);
            try
            {
                using Bitmap bmp = new Bitmap(this.baseIcon.Width, this.baseIcon.Height);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);

                    // Determine background/foreground colors using centralized helper.
                    CalendarEntry? meetingForColor = this.app.GetNextMeetingForUi();
                    double? minutesForColor = meetingForColor is not null ? (meetingForColor.StartTime - now).TotalMinutes : null;
                    (Color bgColor, Color fgColor) = MeetingColorHelper.GetColors(state, minutesForColor);

                    // Full solid background.
                    using Brush bgBrush = new SolidBrush(bgColor);
                    g.FillRectangle(bgBrush, new Rectangle(0, 0, bmp.Width, bmp.Height));

                    // Dynamically find largest font that fits overlay text.
                    int target = bmp.Height; // start from icon height
                    Font? chosen = null;
                    for (int sz = target; sz >= 6; sz -= 2)
                    {
                        using Font test = new Font(FontFamily.GenericSansSerif, sz, FontStyle.Bold, GraphicsUnit.Pixel);
                        SizeF s = g.MeasureString(overlay, test);
                        if (s.Width <= bmp.Width * 0.9 && s.Height <= bmp.Height * 0.9)
                        {
                            chosen = new Font(FontFamily.GenericSansSerif, sz, FontStyle.Bold, GraphicsUnit.Pixel);
                            break;
                        }
                    }

                    chosen ??= new Font(FontFamily.GenericSansSerif, bmp.Height / 3.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                    using (chosen)
                    {
                        SizeF size = g.MeasureString(overlay, chosen);
                        PointF pt = new PointF((bmp.Width - size.Width) / 2f, (bmp.Height - size.Height) / 2f);
                        using Brush fg = new SolidBrush(fgColor);
                        g.DrawString(overlay, chosen, fg, pt);
                    }
                }

                // Dispose previous dynamically generated overlay icon (but never baseIcon clone).
                if (this.notifyIcon.Icon != null && this.notifyIcon.Icon != this.baseIcon)
                {
                    this.notifyIcon.Icon.Dispose();
                }

                this.DestroyLastOverlayHandle();
                IntPtr hIcon = bmp.GetHicon();
                this.lastOverlayIconHandle = hIcon;
                this.notifyIcon.Icon = Icon.FromHandle(hIcon);
            }
            catch (ArgumentException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        private void DestroyLastOverlayHandle()
        {
            if (this.lastOverlayIconHandle != IntPtr.Zero)
            {
                // Win32 resource cleanup; ignore failures.
                try
                {
                    NativeMethods.DestroyIcon(this.lastOverlayIconHandle);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }

                this.lastOverlayIconHandle = IntPtr.Zero;
            }
        }

        private void OnOpenMeetingClick(object? sender, EventArgs e)
        {
            CalendarEntry? next = this.app.GetNextMeetingForUi();
            if (next?.MeetingUrl != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = next.MeetingUrl.ToString(), UseShellExecute = true });
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void OnManualRefreshClick(object? sender, EventArgs e)
        {
            _ = this.RefreshAndUpdateUiAsync();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Using centralized UiText constants; localization pending.")]
        private void ShowBalloon(string message)
        {
            try
            {
                // Use meeting title if available.
                string title = this.app.GetNextMeetingForUi()?.Title ?? UiText.ApplicationTitle;
                this.notifyIcon.BalloonTipTitle = title;
                this.notifyIcon.BalloonTipText = message;
                this.notifyIcon.ShowBalloonTip(3000);
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        private void OnOpenConfigFolderClick(object? sender, EventArgs e)
        {
            try
            {
                string path = System.IO.Path.GetDirectoryName(this.app.GetConfigFilePathForTest()) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnOpenConfigFileClick(object? sender, EventArgs e)
        {
            try
            {
                string file = this.app.GetConfigFilePathForTest();
                if (System.IO.File.Exists(file))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = file, UseShellExecute = true });
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnOpenCalendarUrlClick(object? sender, EventArgs e)
        {
            string url = this.app.GetCalendarUrlForUi();
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void OnCopyCalendarUrlClick(object? sender, EventArgs e)
        {
            string url = this.app.GetCalendarUrlForUi();
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    Clipboard.SetText(url);
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                }
            }
        }

        private void OnCopyMeetingLinkClick(object? sender, EventArgs e)
        {
            CalendarEntry? next = this.app.GetNextMeetingForUi();
            if (next?.MeetingUrl != null)
            {
                try
                {
                    Clipboard.SetText(next.MeetingUrl.ToString());
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                }
            }
        }

        private void OnRefreshIntervalClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is int minutes)
            {
                this.app.SetRefreshMinutes(minutes);
                this.refreshTimer.Interval = Math.Max(1, minutes) * 60_000;

                // Update check marks
                foreach (ToolStripMenuItem opt in item.GetCurrentParent()?.Items.OfType<ToolStripMenuItem>() ?? Enumerable.Empty<ToolStripMenuItem>())
                {
                    if (opt.Tag is int m)
                    {
                        opt.Checked = m == minutes;
                    }
                }

                _ = this.RefreshAndUpdateUiAsync();
            }
        }

        private void OnAboutClick(object? sender, EventArgs e)
        {
            try
            {
                string version = typeof(TrayApplication).Assembly.GetName().Version?.ToString() ?? "?";
                MessageBox.Show(UiText.VersionLabel + " " + version + "\n" + UiText.ApplicationTitle, UiText.About, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        /// <summary>
        /// Native interop methods.
        /// </summary>
        internal static class NativeMethods
        {
            /// <summary>
            /// Destroys an icon handle created with GetHicon.
            /// </summary>
            /// <param name="hIcon">Handle to destroy.</param>
            /// <returns>True on success.</returns>
            [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            internal static extern bool DestroyIcon(IntPtr hIcon);
        }
    }
}
