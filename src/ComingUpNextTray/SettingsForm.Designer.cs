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
    private System.Windows.Forms.Button buttonSave;
    private System.Windows.Forms.Button buttonCancel;

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
        // buttonSave
        // 
    this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.buttonSave.Location = new System.Drawing.Point(329, 80);
        this.buttonSave.Name = "buttonSave";
        this.buttonSave.Size = new System.Drawing.Size(75, 25);
        this.buttonSave.TabIndex = 2;
        this.buttonSave.Text = UiText.Save;
        this.buttonSave.UseVisualStyleBackColor = true;
        this.buttonSave.Click += new System.EventHandler(this.OnSaveClick);
        // 
        // buttonCancel
        // 
    this.buttonCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.buttonCancel.Location = new System.Drawing.Point(410, 80);
        this.buttonCancel.Name = "buttonCancel";
        this.buttonCancel.Size = new System.Drawing.Size(75, 25);
        this.buttonCancel.TabIndex = 3;
        this.buttonCancel.Text = UiText.Cancel;
        this.buttonCancel.UseVisualStyleBackColor = true;
        this.buttonCancel.Click += new System.EventHandler(this.OnCancelClick);
        // 
        // SettingsForm
        // 
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
    this.ClientSize = new System.Drawing.Size(500, 140);
    this.Controls.Add(this.buttonCancel);
    this.Controls.Add(this.buttonSave);
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
