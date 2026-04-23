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
            buttonStop = new Button();
            textBoxLog = new TextBox();
            SuspendLayout();
            // 
            // labelMode
            // 
            labelMode.AutoSize = true;
            labelMode.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelMode.Location = new Point(98, 44);
            labelMode.Name = "labelMode";
            labelMode.Size = new Size(98, 42);
            labelMode.TabIndex = 0;
            labelMode.Text = "label1";
            // 
            // labelSessionId
            // 
            labelSessionId.AutoSize = true;
            labelSessionId.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelSessionId.Location = new Point(577, 44);
            labelSessionId.Name = "labelSessionId";
            labelSessionId.Size = new Size(98, 42);
            labelSessionId.TabIndex = 1;
            labelSessionId.Text = "label1";
            // 
            // labelStatusTitle
            // 
            labelStatusTitle.AutoSize = true;
            labelStatusTitle.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelStatusTitle.Location = new Point(98, 358);
            labelStatusTitle.Name = "labelStatusTitle";
            labelStatusTitle.Size = new Size(98, 42);
            labelStatusTitle.TabIndex = 2;
            labelStatusTitle.Text = "label1";
            // 
            // labelStatusValue
            // 
            labelStatusValue.AutoSize = true;
            labelStatusValue.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            labelStatusValue.Location = new Point(279, 358);
            labelStatusValue.Name = "labelStatusValue";
            labelStatusValue.Size = new Size(98, 42);
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
            buttonAudioOn.Location = new Point(98, 228);
            buttonAudioOn.Name = "buttonAudioOn";
            buttonAudioOn.Size = new Size(162, 59);
            buttonAudioOn.TabIndex = 7;
            buttonAudioOn.Text = "button1";
            buttonAudioOn.UseVisualStyleBackColor = true;
            // 
            // buttonAudioOff
            // 
            buttonAudioOff.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            buttonAudioOff.Location = new Point(333, 228);
            buttonAudioOff.Name = "buttonAudioOff";
            buttonAudioOff.Size = new Size(162, 59);
            buttonAudioOff.TabIndex = 8;
            buttonAudioOff.Text = "button1";
            buttonAudioOff.UseVisualStyleBackColor = true;
            // 
            // buttonStop
            // 
            buttonStop.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            buttonStop.Location = new Point(577, 228);
            buttonStop.Name = "buttonStop";
            buttonStop.Size = new Size(162, 59);
            buttonStop.TabIndex = 9;
            buttonStop.Text = "button1";
            buttonStop.UseVisualStyleBackColor = true;
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
            // Host
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(2287, 1014);
            Controls.Add(textBoxLog);
            Controls.Add(buttonStop);
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
        private Button buttonStop;
        private TextBox textBoxLog;
    }
}
