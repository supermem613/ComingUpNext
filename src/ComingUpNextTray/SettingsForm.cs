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
