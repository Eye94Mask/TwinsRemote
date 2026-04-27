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
            labelMode = new Label();
            labelSessionId = new Label();
            labelStatusTitle = new Label();
            labelStatusValue = new Label();
            comboBoxMode = new ComboBox();
            textBoxSessionId = new TextBox();
            buttonStart = new Button();
            buttonAudioOn = new Button();
            buttonAudioOff = new Button();
            buttonAudioSystem = new Button();
            textBoxLog = new TextBox();
            comboBoxLanguage = new ComboBox();
            SuspendLayout();
            // 
            // labelMode
            // 
            labelMode.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelMode.ImageAlign = ContentAlignment.MiddleRight;
            labelMode.Location = new Point(37, 49);
            labelMode.Name = "labelMode";
            labelMode.RightToLeft = RightToLeft.Yes;
            labelMode.Size = new Size(207, 42);
            labelMode.TabIndex = 0;
            labelMode.Text = "label1";
            labelMode.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelSessionId
            // 
            labelSessionId.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelSessionId.ImageAlign = ContentAlignment.MiddleRight;
            labelSessionId.Location = new Point(438, 49);
            labelSessionId.Name = "labelSessionId";
            labelSessionId.RightToLeft = RightToLeft.Yes;
            labelSessionId.Size = new Size(276, 42);
            labelSessionId.TabIndex = 1;
            labelSessionId.Text = "label1";
            labelSessionId.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelStatusTitle
            // 
            labelStatusTitle.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelStatusTitle.ImageAlign = ContentAlignment.MiddleRight;
            labelStatusTitle.Location = new Point(96, 360);
            labelStatusTitle.Name = "labelStatusTitle";
            labelStatusTitle.RightToLeft = RightToLeft.No;
            labelStatusTitle.Size = new Size(248, 42);
            labelStatusTitle.TabIndex = 2;
            labelStatusTitle.Text = "label1";
            // 
            // labelStatusValue
            // 
            labelStatusValue.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelStatusValue.ImageAlign = ContentAlignment.MiddleLeft;
            labelStatusValue.Location = new Point(350, 360);
            labelStatusValue.Name = "labelStatusValue";
            labelStatusValue.Size = new Size(473, 42);
            labelStatusValue.TabIndex = 3;
            labelStatusValue.Text = "label1";
            // 
            // comboBoxMode
            // 
            comboBoxMode.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            comboBoxMode.FormattingEnabled = true;
            comboBoxMode.Location = new Point(250, 41);
            comboBoxMode.Name = "comboBoxMode";
            comboBoxMode.Size = new Size(182, 50);
            comboBoxMode.TabIndex = 4;
            // 
            // textBoxSessionId
            // 
            textBoxSessionId.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            textBoxSessionId.Location = new Point(720, 41);
            textBoxSessionId.Name = "textBoxSessionId";
            textBoxSessionId.Size = new Size(182, 49);
            textBoxSessionId.TabIndex = 5;
            // 
            // buttonStart
            // 
            buttonStart.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            buttonStart.Location = new Point(1114, 41);
            buttonStart.Name = "buttonStart";
            buttonStart.Size = new Size(162, 59);
            buttonStart.TabIndex = 6;
            buttonStart.Text = "button1";
            buttonStart.UseVisualStyleBackColor = true;
            buttonStart.Click += buttonStart_Click;
            // 
            // buttonAudioOn
            // 
            buttonAudioOn.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            buttonAudioOn.Location = new Point(100, 228);
            buttonAudioOn.Name = "buttonAudioOn";
            buttonAudioOn.Size = new Size(222, 59);
            buttonAudioOn.TabIndex = 7;
            buttonAudioOn.Text = "button1";
            buttonAudioOn.UseVisualStyleBackColor = true;
            buttonAudioOn.Click += buttonAudioOn_Click;
            // 
            // buttonAudioOff
            // 
            buttonAudioOff.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            buttonAudioOff.Location = new Point(600, 228);
            buttonAudioOff.Name = "buttonAudioOff";
            buttonAudioOff.Size = new Size(222, 59);
            buttonAudioOff.TabIndex = 8;
            buttonAudioOff.Text = "button1";
            buttonAudioOff.UseVisualStyleBackColor = true;
            buttonAudioOff.Click += buttonAudioOff_Click;
            // 
            // buttonAudioSystem
            // 
            buttonAudioSystem.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            buttonAudioSystem.Location = new Point(350, 228);
            buttonAudioSystem.Name = "buttonAudioSystem";
            buttonAudioSystem.Size = new Size(222, 59);
            buttonAudioSystem.TabIndex = 9;
            buttonAudioSystem.Text = "button1";
            buttonAudioSystem.UseVisualStyleBackColor = true;
            buttonAudioSystem.Click += buttonAudioSystem_Click;
            // 
            // textBoxLog
            // 
            textBoxLog.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            textBoxLog.Location = new Point(98, 497);
            textBoxLog.Multiline = true;
            textBoxLog.Name = "textBoxLog";
            textBoxLog.Size = new Size(2074, 464);
            textBoxLog.TabIndex = 10;
            // 
            // comboBoxLanguage
            // 
            comboBoxLanguage.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            comboBoxLanguage.FormattingEnabled = true;
            comboBoxLanguage.Location = new Point(2110, 12);
            comboBoxLanguage.Name = "comboBoxLanguage";
            comboBoxLanguage.Size = new Size(182, 50);
            comboBoxLanguage.TabIndex = 11;
            comboBoxLanguage.SelectedIndexChanged += comboBoxLanguage_SelectedIndexChanged;
            // 
            // Host
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(2304, 1014);
            Controls.Add(comboBoxLanguage);
            Controls.Add(textBoxLog);
            Controls.Add(buttonAudioSystem);
            Controls.Add(buttonAudioOff);
            Controls.Add(buttonAudioOn);
            Controls.Add(buttonStart);
            Controls.Add(textBoxSessionId);
            Controls.Add(comboBoxMode);
            Controls.Add(labelStatusValue);
            Controls.Add(labelStatusTitle);
            Controls.Add(labelSessionId);
            Controls.Add(labelMode);
            Name = "Host";
            Text = "TwinsRemote Host";
            Load += Host_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelMode;
        private Label labelSessionId;
        private Label labelStatusTitle;
        private Label labelStatusValue;
        private ComboBox comboBoxMode;
        private TextBox textBoxSessionId;
        private Button buttonStart;
        private Button buttonAudioOn;
        private Button buttonAudioOff;
        private Button buttonAudioSystem;
        private TextBox textBoxLog;
        private ComboBox comboBoxLanguage;
    }
}
