namespace ComingUpNextTray
{
    /// <summary>
    /// Settings dialog host form (renamed from default Form1) for future configuration UI.
    /// </summary>
    internal partial class SettingsForm : Form
    {
        private TrayApplication? app;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsForm"/> class.
        /// </summary>
        public SettingsForm()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Initializes the form with an existing <see cref="TrayApplication"/> instance so user changes can be persisted.
        /// </summary>
        /// <param name="application">Underlying application logic instance.</param>
        internal void Initialize(TrayApplication application)
        {
            this.app = application;

            // Populate current calendar URL.
            string current = this.app.GetCalendarUrlForUi();
            this.textCalendarUrl.Text = current;

            // Populate hover window enabled state
            try
            {
                this.checkShowHoverWindow.Checked = this.app.GetShowHoverWindowForUi();
                try
                {
                    this.checkIgnoreFreeOrFollowing.Checked = this.app.GetIgnoreFreeOrFollowingForUi();
                }
                catch (System.InvalidOperationException)
                {
                    // ignore if app not fully initialized
                }
            }
            catch (System.InvalidOperationException)
            {
                // ignore if app not fully initialized
            }
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            if (this.app is null)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            string input = this.textCalendarUrl.Text?.Trim() ?? string.Empty;

            // Accept empty to clear URL.
            this.app.SetCalendarUrl(input);

            // Persist hover window enabled state
            this.app.SetShowHoverWindow(this.checkShowHoverWindow.Checked);

            // Persist ignore free/following state
            this.app.SetIgnoreFreeOrFollowing(this.checkIgnoreFreeOrFollowing.Checked);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void OnCancelClick(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
