namespace ComingUpNextTray
{
    using System;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;
    using ComingUpNextTray.Models;

    /// <summary>
    /// Small always-on-top movable window that displays the next meeting title and start time.
    /// Background color should be set externally to match tray icon colors.
    /// </summary>
    internal sealed class HoverWindow : Form
    {
        private readonly Label titleLabel;
        private readonly Label timeLabel;
        private Point dragStart;

        /// <summary>
        /// Initializes a new instance of the <see cref="HoverWindow"/> class.
        /// </summary>
        public HoverWindow()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Padding = new Padding(8);
            this.BackColor = Color.Black;

            this.titleLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(8, 8),
            };

            this.timeLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(8, 28),
            };

            this.Controls.Add(this.titleLabel);
            this.Controls.Add(this.timeLabel);

            // Allow dragging by mouse down anywhere on the form.
            this.MouseDown += this.OnMouseDown_Move;
            foreach (Control c in this.Controls)
            {
                c.MouseDown += this.OnMouseDown_Move;
            }

            // Default size
            this.Size = new Size(220, 60);
        }

        /// <summary>
        /// Updates displayed meeting information.
        /// </summary>
        /// <param name="meeting">Next meeting or null.</param>
        /// <param name="now">Reference time for formatting.</param>
        /// <param name="overlayToken">Optional overlay token (e.g. "5" or "1h") to display after the title as "(In X)".</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1303:Do not pass literals as localized parameters", Justification = "Using centralized UiText constants; localization pending.")]
        public void UpdateMeeting(CalendarEntry? meeting, DateTime now, string? overlayToken = null)
        {
            if (meeting is null)
            {
                this.titleLabel.Text = UiText.NoUpcomingMeetings;
                this.timeLabel.Text = string.Empty;
            }
            else
            {
                string title = meeting.Title ?? "Untitled";

                // Only append "(In X)" for meaningful overlay tokens (numbers or hour tokens).
                // Suppress for non-minute symbols like '?', '-' or the infinity symbol.
                if (!string.IsNullOrEmpty(overlayToken)
                    && overlayToken != UiText.InfiniteSymbol
                    && overlayToken != "?"
                    && overlayToken != "-")
                {
                    this.titleLabel.Text = $"{title} (in {overlayToken})";
                }
                else
                {
                    this.titleLabel.Text = title;
                }

                // If meeting is today, show just the time; otherwise include day-of-week.
                string timeFormat = meeting.StartTime.Date == now.Date ? "h:mm tt" : "ddd h:mm tt";
                this.timeLabel.Text = meeting.StartTime.ToString(timeFormat, CultureInfo.CurrentCulture);
            }

            // Resize to fit
            int width = Math.Max(this.titleLabel.Width, this.timeLabel.Width) + this.Padding.Horizontal + 8;
            int height = this.timeLabel.Bottom + this.Padding.Bottom + 8;
            this.Size = new Size(Math.Min(width, 420), height);
        }

        /// <summary>
        /// Sets the background and foreground to match the specified colors.
        /// </summary>
        /// <param name="background">Background color.</param>
        /// <param name="foreground">Foreground color.</param>
        public void SetColors(Color background, Color foreground)
        {
            this.BackColor = background;
            this.titleLabel.ForeColor = foreground;
            this.timeLabel.ForeColor = foreground;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.titleLabel.Dispose();
                this.timeLabel.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnMouseDown_Move(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.dragStart = new Point(e.X, e.Y);
                this.Capture = true;
                this.MouseMove += this.OnMouseMove_Drag;
                this.MouseUp += this.OnMouseUp_EndDrag;
            }
        }

        private void OnMouseMove_Drag(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Point p = this.PointToScreen(e.Location);
                this.Location = new Point(p.X - this.dragStart.X, p.Y - this.dragStart.Y);
            }
        }

        private void OnMouseUp_EndDrag(object? sender, MouseEventArgs e)
        {
            this.MouseMove -= this.OnMouseMove_Drag;
            this.MouseUp -= this.OnMouseUp_EndDrag;
            this.Capture = false;
        }
    }
}
