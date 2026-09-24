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
            this.comboCalendarSource.SelectedIndex = this.app.GetCalendarSourceForUi() == Models.CalendarSourceKind.WorkIq ? 1 : 0;
            this.textWorkIqAccount.Text = this.app.GetWorkIqAccountForUi();
            this.textWorkIqExecutablePath.Text = this.app.GetWorkIqExecutablePathForUi();
            this.UpdateCalendarSourceControls();

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

            // Populate sound intro path
            this.textSoundIntroPath.Text = this.app.GetSoundIntroPathForUi() ?? string.Empty;
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
            Models.CalendarSourceKind source = this.comboCalendarSource.SelectedIndex == 1
                ? Models.CalendarSourceKind.WorkIq
                : Models.CalendarSourceKind.Ics;
            string workIqAccount = this.textWorkIqAccount.Text?.Trim() ?? string.Empty;
            if (source == Models.CalendarSourceKind.WorkIq && string.IsNullOrWhiteSpace(workIqAccount))
            {
                System.Windows.Forms.MessageBox.Show(UiText.WorkIqAccountRequired, UiText.ConfigErrorTitle, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                this.textWorkIqAccount.Focus();
                return;
            }

            try
            {
                // Save all config values at once - this updates both memory and disk
                // Note: SaveConfig updates in-memory values before writing to disk
                string soundPath = this.textSoundIntroPath.Text?.Trim() ?? string.Empty;
                string workIqExecutablePath = this.textWorkIqExecutablePath.Text?.Trim() ?? string.Empty;
                this.app.SaveConfig(new Models.ConfigModel
                {
                    CalendarUrl = input,
                    CalendarSource = source,
                    WorkIqAccount = string.IsNullOrWhiteSpace(workIqAccount) ? null : workIqAccount,
                    WorkIqExecutablePath = string.IsNullOrWhiteSpace(workIqExecutablePath) ? null : workIqExecutablePath,
                    RefreshMinutes = this.app.GetRefreshMinutesForUi(),
                    ShowHoverWindow = this.checkShowHoverWindow.Checked,
                    IgnoreFreeOrFollowing = this.checkIgnoreFreeOrFollowing.Checked,
                    HoverWindowLeft = this.app.GetHoverWindowLeftForUi(),
                    HoverWindowTop = this.app.GetHoverWindowTopForUi(),
                    SoundIntroPath = string.IsNullOrWhiteSpace(soundPath) ? null : soundPath,
                });

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (InvalidOperationException ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Using centralized UiText constants; localization pending.")]
        private void OnBrowseSoundIntroClick(object? sender, EventArgs e)
        {
            using OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Select Sound Intro MP3",
                Filter = "MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*",
                CheckFileExists = true,
            };

            if (!string.IsNullOrWhiteSpace(this.textSoundIntroPath.Text) && System.IO.File.Exists(this.textSoundIntroPath.Text))
            {
                dlg.InitialDirectory = System.IO.Path.GetDirectoryName(this.textSoundIntroPath.Text);
            }

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                this.textSoundIntroPath.Text = dlg.FileName;
            }
        }

        private void OnCancelClick(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void OnCalendarSourceChanged(object? sender, EventArgs e)
        {
            this.UpdateCalendarSourceControls();
        }

        private void UpdateCalendarSourceControls()
        {
            bool useWorkIq = this.comboCalendarSource.SelectedIndex == 1;
            this.labelCalendarUrl.Enabled = !useWorkIq;
            this.textCalendarUrl.Enabled = !useWorkIq;
            this.labelWorkIqAccount.Enabled = useWorkIq;
            this.textWorkIqAccount.Enabled = useWorkIq;
            this.labelWorkIqExecutablePath.Enabled = useWorkIq;
            this.textWorkIqExecutablePath.Enabled = useWorkIq;
            this.buttonBrowseWorkIqExecutable.Enabled = useWorkIq;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Using centralized UiText constants; localization pending.")]
        private void OnBrowseWorkIqExecutableClick(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog
            {
                Title = UiText.SelectWorkIqExecutable,
                Filter = "Executable files (*.exe)|*.exe",
                CheckFileExists = true,
            };

            string currentPath = this.textWorkIqExecutablePath.Text;
            if (!string.IsNullOrWhiteSpace(currentPath) && System.IO.File.Exists(currentPath))
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(currentPath);
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                this.textWorkIqExecutablePath.Text = dialog.FileName;
            }
        }
    }
}
