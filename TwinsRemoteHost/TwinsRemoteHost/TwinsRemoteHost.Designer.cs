namespace TwinsRemoteHost
{
    partial class Host
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Host));
            modeLabel = new Label();
            sessionIdLabel = new Label();
            statusTitleLabel = new Label();
            statusValueLabel = new Label();
            modeComboBox = new ComboBox();
            sessionIdTextBox = new TextBox();
            connectButton = new Button();
            audioOnButton = new Button();
            audioOffButton = new Button();
            audioSystemButton = new Button();
            logTextBox = new TextBox();
            languageComboBox = new ComboBox();
            createCustomModeButton = new Button();
            updateCustomMode = new Button();
            infoBellPictureBox = new PictureBox();
            notificationCountLabel = new Label();
            updateLabel = new Label();
            saveLogButton = new Button();
            audioLabel = new Label();
            ((System.ComponentModel.ISupportInitialize)infoBellPictureBox).BeginInit();
            SuspendLayout();
            // 
            // modeLabel
            // 
            modeLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            modeLabel.ImageAlign = ContentAlignment.MiddleRight;
            modeLabel.Location = new Point(96, 49);
            modeLabel.Name = "modeLabel";
            modeLabel.RightToLeft = RightToLeft.Yes;
            modeLabel.Size = new Size(207, 42);
            modeLabel.TabIndex = 0;
            modeLabel.Text = "label1";
            modeLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // sessionIdLabel
            // 
            sessionIdLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            sessionIdLabel.ImageAlign = ContentAlignment.MiddleRight;
            sessionIdLabel.Location = new Point(567, 49);
            sessionIdLabel.Name = "sessionIdLabel";
            sessionIdLabel.RightToLeft = RightToLeft.Yes;
            sessionIdLabel.Size = new Size(276, 42);
            sessionIdLabel.TabIndex = 1;
            sessionIdLabel.Text = "label1";
            sessionIdLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // statusTitleLabel
            // 
            statusTitleLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            statusTitleLabel.ImageAlign = ContentAlignment.MiddleRight;
            statusTitleLabel.Location = new Point(96, 360);
            statusTitleLabel.Name = "statusTitleLabel";
            statusTitleLabel.RightToLeft = RightToLeft.No;
            statusTitleLabel.Size = new Size(248, 42);
            statusTitleLabel.TabIndex = 2;
            statusTitleLabel.Text = "label1";
            // 
            // statusValueLabel
            // 
            statusValueLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            statusValueLabel.ImageAlign = ContentAlignment.MiddleLeft;
            statusValueLabel.Location = new Point(350, 360);
            statusValueLabel.Name = "statusValueLabel";
            statusValueLabel.Size = new Size(473, 42);
            statusValueLabel.TabIndex = 3;
            statusValueLabel.Text = "label1";
            // 
            // modeComboBox
            // 
            modeComboBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            modeComboBox.FormattingEnabled = true;
            modeComboBox.Location = new Point(309, 41);
            modeComboBox.Name = "modeComboBox";
            modeComboBox.Size = new Size(322, 50);
            modeComboBox.TabIndex = 4;
            // 
            // sessionIdTextBox
            // 
            sessionIdTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            sessionIdTextBox.Location = new Point(849, 41);
            sessionIdTextBox.Name = "sessionIdTextBox";
            sessionIdTextBox.Size = new Size(318, 49);
            sessionIdTextBox.TabIndex = 5;
            // 
            // connectButton
            // 
            connectButton.BackColor = Color.White;
            connectButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            connectButton.Location = new Point(1228, 41);
            connectButton.Name = "connectButton";
            connectButton.Size = new Size(162, 59);
            connectButton.TabIndex = 6;
            connectButton.Text = "button1";
            connectButton.UseVisualStyleBackColor = false;
            connectButton.Click += connectButton_Click;
            // 
            // audioOnButton
            // 
            audioOnButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            audioOnButton.Location = new Point(100, 228);
            audioOnButton.Name = "audioOnButton";
            audioOnButton.Size = new Size(222, 59);
            audioOnButton.TabIndex = 7;
            audioOnButton.TabStop = false;
            audioOnButton.Text = "button1";
            audioOnButton.UseVisualStyleBackColor = true;
            audioOnButton.Click += buttonAudioOn_Click;
            // 
            // audioOffButton
            // 
            audioOffButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            audioOffButton.Location = new Point(600, 228);
            audioOffButton.Name = "audioOffButton";
            audioOffButton.Size = new Size(222, 59);
            audioOffButton.TabIndex = 8;
            audioOffButton.TabStop = false;
            audioOffButton.Text = "button1";
            audioOffButton.UseVisualStyleBackColor = true;
            audioOffButton.Click += buttonAudioOff_Click;
            // 
            // audioSystemButton
            // 
            audioSystemButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            audioSystemButton.Location = new Point(350, 228);
            audioSystemButton.Name = "audioSystemButton";
            audioSystemButton.Size = new Size(222, 59);
            audioSystemButton.TabIndex = 9;
            audioSystemButton.TabStop = false;
            audioSystemButton.Text = "button1";
            audioSystemButton.UseVisualStyleBackColor = true;
            audioSystemButton.Click += buttonAudioSystem_Click;
            // 
            // logTextBox
            // 
            logTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            logTextBox.Location = new Point(98, 497);
            logTextBox.Multiline = true;
            logTextBox.Name = "logTextBox";
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(2074, 464);
            logTextBox.TabIndex = 10;
            // 
            // languageComboBox
            // 
            languageComboBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            languageComboBox.FormattingEnabled = true;
            languageComboBox.Location = new Point(2110, 12);
            languageComboBox.Name = "languageComboBox";
            languageComboBox.Size = new Size(182, 50);
            languageComboBox.TabIndex = 11;
            languageComboBox.TabStop = false;
            languageComboBox.SelectedIndexChanged += languageComboBox_SelectedIndexChanged;
            // 
            // createCustomModeButton
            // 
            createCustomModeButton.BackColor = Color.White;
            createCustomModeButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            createCustomModeButton.Location = new Point(1887, 88);
            createCustomModeButton.Name = "createCustomModeButton";
            createCustomModeButton.Size = new Size(405, 59);
            createCustomModeButton.TabIndex = 12;
            createCustomModeButton.TabStop = false;
            createCustomModeButton.Text = "button1";
            createCustomModeButton.UseVisualStyleBackColor = false;
            createCustomModeButton.Click += createMode_Click;
            // 
            // updateCustomMode
            // 
            updateCustomMode.BackColor = Color.White;
            updateCustomMode.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            updateCustomMode.Location = new Point(1887, 178);
            updateCustomMode.Name = "updateCustomMode";
            updateCustomMode.Size = new Size(405, 59);
            updateCustomMode.TabIndex = 13;
            updateCustomMode.TabStop = false;
            updateCustomMode.Text = "button1";
            updateCustomMode.UseVisualStyleBackColor = false;
            updateCustomMode.Click += updateDeleteCustomMode_Click;
            // 
            // infoBellPictureBox
            // 
            infoBellPictureBox.BackColor = SystemColors.Control;
            infoBellPictureBox.Enabled = false;
            infoBellPictureBox.ErrorImage = null;
            infoBellPictureBox.Image = (Image)resources.GetObject("infoBellPictureBox.Image");
            infoBellPictureBox.Location = new Point(1958, 9);
            infoBellPictureBox.Name = "infoBellPictureBox";
            infoBellPictureBox.Size = new Size(80, 67);
            infoBellPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            infoBellPictureBox.TabIndex = 14;
            infoBellPictureBox.TabStop = false;
            infoBellPictureBox.Click += infoBellPictureBox_Click;
            // 
            // notificationCountLabel
            // 
            notificationCountLabel.AutoSize = true;
            notificationCountLabel.BackColor = Color.Red;
            notificationCountLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            notificationCountLabel.ForeColor = Color.Cornsilk;
            notificationCountLabel.Location = new Point(1930, -2);
            notificationCountLabel.Name = "notificationCountLabel";
            notificationCountLabel.Size = new Size(0, 42);
            notificationCountLabel.TabIndex = 15;
            notificationCountLabel.TextAlign = ContentAlignment.MiddleCenter;
            notificationCountLabel.Click += notificationCountLabel_Click;
            // 
            // updateLabel
            // 
            updateLabel.AutoSize = true;
            updateLabel.BackColor = Color.LimeGreen;
            updateLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            updateLabel.ForeColor = Color.Cornsilk;
            updateLabel.Location = new Point(1930, 44);
            updateLabel.Name = "updateLabel";
            updateLabel.Size = new Size(0, 42);
            updateLabel.TabIndex = 16;
            updateLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // saveLogButton
            // 
            saveLogButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            saveLogButton.Location = new Point(100, 979);
            saveLogButton.Name = "saveLogButton";
            saveLogButton.Size = new Size(222, 59);
            saveLogButton.TabIndex = 17;
            saveLogButton.TabStop = false;
            saveLogButton.Text = "button1";
            saveLogButton.UseVisualStyleBackColor = true;
            saveLogButton.Click += saveLogButton_Click;
            // 
            // audioLabel
            // 
            audioLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            audioLabel.ImageAlign = ContentAlignment.MiddleRight;
            audioLabel.Location = new Point(100, 162);
            audioLabel.Name = "audioLabel";
            audioLabel.RightToLeft = RightToLeft.No;
            audioLabel.Size = new Size(248, 42);
            audioLabel.TabIndex = 18;
            audioLabel.Text = "label1";
            // 
            // Host
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(2304, 1050);
            Controls.Add(audioLabel);
            Controls.Add(saveLogButton);
            Controls.Add(updateLabel);
            Controls.Add(notificationCountLabel);
            Controls.Add(infoBellPictureBox);
            Controls.Add(updateCustomMode);
            Controls.Add(createCustomModeButton);
            Controls.Add(languageComboBox);
            Controls.Add(logTextBox);
            Controls.Add(audioSystemButton);
            Controls.Add(audioOffButton);
            Controls.Add(audioOnButton);
            Controls.Add(connectButton);
            Controls.Add(sessionIdTextBox);
            Controls.Add(modeComboBox);
            Controls.Add(statusValueLabel);
            Controls.Add(statusTitleLabel);
            Controls.Add(sessionIdLabel);
            Controls.Add(modeLabel);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Host";
            Text = "TwinsRemote Host";
            Load += Host_Load;
            ((System.ComponentModel.ISupportInitialize)infoBellPictureBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label modeLabel;
        private Label sessionIdLabel;
        private Label statusTitleLabel;
        private Label statusValueLabel;
        private ComboBox modeComboBox;
        private TextBox sessionIdTextBox;
        private Button connectButton;
        private Button audioOnButton;
        private Button audioOffButton;
        private Button audioSystemButton;
        private TextBox logTextBox;
        private ComboBox languageComboBox;
        private Button createCustomModeButton;
        private Button updateCustomMode;
        private PictureBox infoBellPictureBox;
        private Label notificationCountLabel;
        private Label updateLabel;
        private Button saveLogButton;
        private Label audioLabel;
    }
}
