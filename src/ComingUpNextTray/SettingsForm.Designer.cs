namespace ComingUpNextTray;

partial class SettingsForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Label labelCalendarUrl;
    private System.Windows.Forms.TextBox textCalendarUrl;
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
        this.labelCalendarUrl = new System.Windows.Forms.Label();
        this.textCalendarUrl = new System.Windows.Forms.TextBox();
        this.buttonSave = new System.Windows.Forms.Button();
        this.buttonCancel = new System.Windows.Forms.Button();
        this.SuspendLayout();
        // 
        // labelCalendarUrl
        // 
        this.labelCalendarUrl.AutoSize = true;
        this.labelCalendarUrl.Location = new System.Drawing.Point(12, 15);
        this.labelCalendarUrl.Name = "labelCalendarUrl";
        this.labelCalendarUrl.Size = new System.Drawing.Size(113, 15);
        this.labelCalendarUrl.TabIndex = 0;
        this.labelCalendarUrl.Text = UiText.SetCalendarUrl + ":";
        // 
        // textCalendarUrl
        // 
        this.textCalendarUrl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textCalendarUrl.Location = new System.Drawing.Point(15, 35);
        this.textCalendarUrl.Name = "textCalendarUrl";
        this.textCalendarUrl.Size = new System.Drawing.Size(470, 23);
        this.textCalendarUrl.TabIndex = 1;
        // 
        // checkShowHoverWindow
        // 
        this.checkShowHoverWindow = new System.Windows.Forms.CheckBox();
        this.checkShowHoverWindow.AutoSize = true;
        this.checkShowHoverWindow.Location = new System.Drawing.Point(15, 64);
        this.checkShowHoverWindow.Name = "checkShowHoverWindow";
        this.checkShowHoverWindow.Size = new System.Drawing.Size(150, 19);
        this.checkShowHoverWindow.TabIndex = 4;
        this.checkShowHoverWindow.Text = UiText.ToggleHoverWindow;
        this.checkShowHoverWindow.UseVisualStyleBackColor = true;
        // 
        // checkIgnoreFreeOrFollowing
        // 
        this.checkIgnoreFreeOrFollowing = new System.Windows.Forms.CheckBox();
        this.checkIgnoreFreeOrFollowing.AutoSize = true;
        this.checkIgnoreFreeOrFollowing.Location = new System.Drawing.Point(15, 90);
        this.checkIgnoreFreeOrFollowing.Name = "checkIgnoreFreeOrFollowing";
        this.checkIgnoreFreeOrFollowing.Size = new System.Drawing.Size(260, 19);
        this.checkIgnoreFreeOrFollowing.TabIndex = 5;
        this.checkIgnoreFreeOrFollowing.Text = "Ignore meetings marked as Free or Following";
        this.checkIgnoreFreeOrFollowing.UseVisualStyleBackColor = true;
        //
        // labelSoundIntro
        //
        this.labelSoundIntro = new System.Windows.Forms.Label();
        this.labelSoundIntro.AutoSize = true;
        this.labelSoundIntro.Location = new System.Drawing.Point(12, 118);
        this.labelSoundIntro.Name = "labelSoundIntro";
        this.labelSoundIntro.Size = new System.Drawing.Size(200, 15);
        this.labelSoundIntro.TabIndex = 6;
        this.labelSoundIntro.Text = "Sound Intro (MP3 played before meeting):";
        //
        // textSoundIntroPath
        //
        this.textSoundIntroPath = new System.Windows.Forms.TextBox();
        this.textSoundIntroPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textSoundIntroPath.Location = new System.Drawing.Point(15, 138);
        this.textSoundIntroPath.Name = "textSoundIntroPath";
        this.textSoundIntroPath.Size = new System.Drawing.Size(380, 23);
        this.textSoundIntroPath.TabIndex = 7;
        //
        // buttonBrowseSoundIntro
        //
        this.buttonBrowseSoundIntro = new System.Windows.Forms.Button();
        this.buttonBrowseSoundIntro.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.buttonBrowseSoundIntro.Location = new System.Drawing.Point(401, 137);
        this.buttonBrowseSoundIntro.Name = "buttonBrowseSoundIntro";
        this.buttonBrowseSoundIntro.Size = new System.Drawing.Size(84, 25);
        this.buttonBrowseSoundIntro.TabIndex = 8;
        this.buttonBrowseSoundIntro.Text = "Browse...";
        this.buttonBrowseSoundIntro.UseVisualStyleBackColor = true;
        this.buttonBrowseSoundIntro.Click += new System.EventHandler(this.OnBrowseSoundIntroClick);
        //
        // buttonSave
        //
        this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.buttonSave.Location = new System.Drawing.Point(329, 175);
        this.buttonSave.Name = "buttonSave";
        this.buttonSave.Size = new System.Drawing.Size(75, 25);
        this.buttonSave.TabIndex = 9;
        this.buttonSave.Text = UiText.Save;
        this.buttonSave.UseVisualStyleBackColor = true;
        this.buttonSave.Click += new System.EventHandler(this.OnSaveClick);
        //
        // buttonCancel
        //
        this.buttonCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.buttonCancel.Location = new System.Drawing.Point(410, 175);
        this.buttonCancel.Name = "buttonCancel";
        this.buttonCancel.Size = new System.Drawing.Size(75, 25);
        this.buttonCancel.TabIndex = 10;
        this.buttonCancel.Text = UiText.Cancel;
        this.buttonCancel.UseVisualStyleBackColor = true;
        this.buttonCancel.Click += new System.EventHandler(this.OnCancelClick);
        // 
        // SettingsForm
        // 
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(500, 215);
        this.Controls.Add(this.buttonCancel);
        this.Controls.Add(this.buttonSave);
        this.Controls.Add(this.buttonBrowseSoundIntro);
        this.Controls.Add(this.textSoundIntroPath);
        this.Controls.Add(this.labelSoundIntro);
        this.Controls.Add(this.checkIgnoreFreeOrFollowing);
        this.Controls.Add(this.checkShowHoverWindow);
        this.Controls.Add(this.textCalendarUrl);
        this.Controls.Add(this.labelCalendarUrl);
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
