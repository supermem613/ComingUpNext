namespace ComingUpNextTray;

partial class SettingsForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Label labelCalendarUrl;
    private System.Windows.Forms.Label labelCalendarSource;
    private System.Windows.Forms.ComboBox comboCalendarSource;
    private System.Windows.Forms.TextBox textCalendarUrl;
    private System.Windows.Forms.Label labelWorkIqAccount;
    private System.Windows.Forms.TextBox textWorkIqAccount;
    private System.Windows.Forms.Label labelWorkIqExecutablePath;
    private System.Windows.Forms.TextBox textWorkIqExecutablePath;
    private System.Windows.Forms.Button buttonBrowseWorkIqExecutable;
    private System.Windows.Forms.CheckBox checkShowHoverWindow;
    private System.Windows.Forms.CheckBox checkIgnoreFreeOrFollowing;
    private System.Windows.Forms.Button buttonSave;
    private System.Windows.Forms.Button buttonCancel;
    private System.Windows.Forms.Label labelSoundIntro;
    private System.Windows.Forms.TextBox textSoundIntroPath;
    private System.Windows.Forms.Button buttonBrowseSoundIntro;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.labelCalendarSource = new System.Windows.Forms.Label();
        this.comboCalendarSource = new System.Windows.Forms.ComboBox();
        this.labelCalendarUrl = new System.Windows.Forms.Label();
        this.textCalendarUrl = new System.Windows.Forms.TextBox();
        this.labelWorkIqAccount = new System.Windows.Forms.Label();
        this.textWorkIqAccount = new System.Windows.Forms.TextBox();
        this.labelWorkIqExecutablePath = new System.Windows.Forms.Label();
        this.textWorkIqExecutablePath = new System.Windows.Forms.TextBox();
        this.buttonBrowseWorkIqExecutable = new System.Windows.Forms.Button();
        this.buttonSave = new System.Windows.Forms.Button();
        this.buttonCancel = new System.Windows.Forms.Button();
        this.SuspendLayout();
        // 
        // labelCalendarSource
        // 
        this.labelCalendarSource.AutoSize = true;
        this.labelCalendarSource.Location = new System.Drawing.Point(12, 15);
        this.labelCalendarSource.Name = "labelCalendarSource";
        this.labelCalendarSource.Size = new System.Drawing.Size(92, 15);
        this.labelCalendarSource.TabIndex = 0;
        this.labelCalendarSource.Text = UiText.CalendarSource + ":";
        // 
        // comboCalendarSource
        // 
        this.comboCalendarSource.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.comboCalendarSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.comboCalendarSource.FormattingEnabled = true;
        this.comboCalendarSource.Items.AddRange(new object[] { UiText.IcsSource, UiText.WorkIqSource });
        this.comboCalendarSource.Location = new System.Drawing.Point(130, 11);
        this.comboCalendarSource.Name = "comboCalendarSource";
        this.comboCalendarSource.Size = new System.Drawing.Size(355, 23);
        this.comboCalendarSource.TabIndex = 1;
        this.comboCalendarSource.SelectedIndex = 0;
        this.comboCalendarSource.SelectedIndexChanged += new System.EventHandler(this.OnCalendarSourceChanged);
        // 
        // labelCalendarUrl
        // 
        this.labelCalendarUrl.AutoSize = true;
        this.labelCalendarUrl.Location = new System.Drawing.Point(12, 47);
        this.labelCalendarUrl.Name = "labelCalendarUrl";
        this.labelCalendarUrl.Size = new System.Drawing.Size(140, 15);
        this.labelCalendarUrl.TabIndex = 2;
        this.labelCalendarUrl.Text = UiText.SetCalendarUrl + " (ICS):";
        // 
        // textCalendarUrl
        // 
        this.textCalendarUrl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textCalendarUrl.Location = new System.Drawing.Point(15, 67);
        this.textCalendarUrl.Name = "textCalendarUrl";
        this.textCalendarUrl.Size = new System.Drawing.Size(470, 23);
        this.textCalendarUrl.TabIndex = 3;
        // 
        // labelWorkIqAccount
        // 
        this.labelWorkIqAccount.AutoSize = true;
        this.labelWorkIqAccount.Location = new System.Drawing.Point(12, 97);
        this.labelWorkIqAccount.Name = "labelWorkIqAccount";
        this.labelWorkIqAccount.Size = new System.Drawing.Size(130, 15);
        this.labelWorkIqAccount.TabIndex = 4;
        this.labelWorkIqAccount.Text = UiText.WorkIqAccount + ":";
        // 
        // textWorkIqAccount
        // 
        this.textWorkIqAccount.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textWorkIqAccount.Location = new System.Drawing.Point(15, 117);
        this.textWorkIqAccount.Name = "textWorkIqAccount";
        this.textWorkIqAccount.Size = new System.Drawing.Size(470, 23);
        this.textWorkIqAccount.TabIndex = 5;
        // 
        // labelWorkIqExecutablePath
        // 
        this.labelWorkIqExecutablePath.AutoSize = true;
        this.labelWorkIqExecutablePath.Location = new System.Drawing.Point(12, 147);
        this.labelWorkIqExecutablePath.Name = "labelWorkIqExecutablePath";
        this.labelWorkIqExecutablePath.Size = new System.Drawing.Size(147, 15);
        this.labelWorkIqExecutablePath.TabIndex = 6;
        this.labelWorkIqExecutablePath.Text = UiText.WorkIqExecutablePath + ":";
        // 
        // textWorkIqExecutablePath
        // 
        this.textWorkIqExecutablePath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textWorkIqExecutablePath.Location = new System.Drawing.Point(15, 167);
        this.textWorkIqExecutablePath.Name = "textWorkIqExecutablePath";
        this.textWorkIqExecutablePath.Size = new System.Drawing.Size(380, 23);
        this.textWorkIqExecutablePath.TabIndex = 7;
        // 
        // buttonBrowseWorkIqExecutable
        // 
        this.buttonBrowseWorkIqExecutable.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.buttonBrowseWorkIqExecutable.Location = new System.Drawing.Point(401, 166);
        this.buttonBrowseWorkIqExecutable.Name = "buttonBrowseWorkIqExecutable";
        this.buttonBrowseWorkIqExecutable.Size = new System.Drawing.Size(84, 25);
        this.buttonBrowseWorkIqExecutable.TabIndex = 8;
        this.buttonBrowseWorkIqExecutable.Text = UiText.Browse;
        this.buttonBrowseWorkIqExecutable.UseVisualStyleBackColor = true;
        this.buttonBrowseWorkIqExecutable.Click += new System.EventHandler(this.OnBrowseWorkIqExecutableClick);
        // 
        // checkShowHoverWindow
        // 
        this.checkShowHoverWindow = new System.Windows.Forms.CheckBox();
        this.checkShowHoverWindow.AutoSize = true;
        this.checkShowHoverWindow.Location = new System.Drawing.Point(15, 199);
        this.checkShowHoverWindow.Name = "checkShowHoverWindow";
        this.checkShowHoverWindow.Size = new System.Drawing.Size(150, 19);
        this.checkShowHoverWindow.TabIndex = 9;
        this.checkShowHoverWindow.Text = UiText.ToggleHoverWindow;
        this.checkShowHoverWindow.UseVisualStyleBackColor = true;
        // 
        // checkIgnoreFreeOrFollowing
        // 
        this.checkIgnoreFreeOrFollowing = new System.Windows.Forms.CheckBox();
        this.checkIgnoreFreeOrFollowing.AutoSize = true;
        this.checkIgnoreFreeOrFollowing.Location = new System.Drawing.Point(15, 225);
        this.checkIgnoreFreeOrFollowing.Name = "checkIgnoreFreeOrFollowing";
        this.checkIgnoreFreeOrFollowing.Size = new System.Drawing.Size(260, 19);
        this.checkIgnoreFreeOrFollowing.TabIndex = 10;
        this.checkIgnoreFreeOrFollowing.Text = "Ignore meetings marked as Free or Following";
        this.checkIgnoreFreeOrFollowing.UseVisualStyleBackColor = true;
        //
        // labelSoundIntro
        //
        this.labelSoundIntro = new System.Windows.Forms.Label();
        this.labelSoundIntro.AutoSize = true;
        this.labelSoundIntro.Location = new System.Drawing.Point(12, 253);
        this.labelSoundIntro.Name = "labelSoundIntro";
        this.labelSoundIntro.Size = new System.Drawing.Size(200, 15);
        this.labelSoundIntro.TabIndex = 11;
        this.labelSoundIntro.Text = "Sound Intro (MP3 played before meeting):";
        //
        // textSoundIntroPath
        //
        this.textSoundIntroPath = new System.Windows.Forms.TextBox();
        this.textSoundIntroPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textSoundIntroPath.Location = new System.Drawing.Point(15, 273);
        this.textSoundIntroPath.Name = "textSoundIntroPath";
        this.textSoundIntroPath.Size = new System.Drawing.Size(380, 23);
        this.textSoundIntroPath.TabIndex = 12;
        //
        // buttonBrowseSoundIntro
        //
        this.buttonBrowseSoundIntro = new System.Windows.Forms.Button();
        this.buttonBrowseSoundIntro.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.buttonBrowseSoundIntro.Location = new System.Drawing.Point(401, 272);
        this.buttonBrowseSoundIntro.Name = "buttonBrowseSoundIntro";
        this.buttonBrowseSoundIntro.Size = new System.Drawing.Size(84, 25);
        this.buttonBrowseSoundIntro.TabIndex = 13;
        this.buttonBrowseSoundIntro.Text = UiText.Browse;
        this.buttonBrowseSoundIntro.UseVisualStyleBackColor = true;
        this.buttonBrowseSoundIntro.Click += new System.EventHandler(this.OnBrowseSoundIntroClick);
        //
        // buttonSave
        //
        this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.buttonSave.Location = new System.Drawing.Point(329, 320);
        this.buttonSave.Name = "buttonSave";
        this.buttonSave.Size = new System.Drawing.Size(75, 25);
        this.buttonSave.TabIndex = 14;
        this.buttonSave.Text = UiText.Save;
        this.buttonSave.UseVisualStyleBackColor = true;
        this.buttonSave.Click += new System.EventHandler(this.OnSaveClick);
        //
        // buttonCancel
        //
        this.buttonCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.buttonCancel.Location = new System.Drawing.Point(410, 320);
        this.buttonCancel.Name = "buttonCancel";
        this.buttonCancel.Size = new System.Drawing.Size(75, 25);
        this.buttonCancel.TabIndex = 15;
        this.buttonCancel.Text = UiText.Cancel;
        this.buttonCancel.UseVisualStyleBackColor = true;
        this.buttonCancel.Click += new System.EventHandler(this.OnCancelClick);
        // 
        // SettingsForm
        // 
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(500, 360);
        this.Controls.Add(this.buttonCancel);
        this.Controls.Add(this.buttonSave);
        this.Controls.Add(this.buttonBrowseSoundIntro);
        this.Controls.Add(this.textSoundIntroPath);
        this.Controls.Add(this.labelSoundIntro);
        this.Controls.Add(this.checkIgnoreFreeOrFollowing);
        this.Controls.Add(this.checkShowHoverWindow);
        this.Controls.Add(this.buttonBrowseWorkIqExecutable);
        this.Controls.Add(this.textWorkIqExecutablePath);
        this.Controls.Add(this.labelWorkIqExecutablePath);
        this.Controls.Add(this.textWorkIqAccount);
        this.Controls.Add(this.labelWorkIqAccount);
        this.Controls.Add(this.textCalendarUrl);
        this.Controls.Add(this.labelCalendarUrl);
        this.Controls.Add(this.comboCalendarSource);
        this.Controls.Add(this.labelCalendarSource);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Settings";
        this.AcceptButton = this.buttonSave;
        this.CancelButton = this.buttonCancel;
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion
}
