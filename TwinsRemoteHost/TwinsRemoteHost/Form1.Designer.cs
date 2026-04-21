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
            streamModeDesc = new TextBox();
            radioBalanced = new RadioButton();
            radioQuality = new RadioButton();
            radioStable = new RadioButton();
            radioMobile = new RadioButton();
            SuspendLayout();
            // 
            // streamModeDesc
            // 
            streamModeDesc.BackColor = SystemColors.HighlightText;
            streamModeDesc.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            streamModeDesc.Location = new Point(120, 70);
            streamModeDesc.Name = "streamModeDesc";
            streamModeDesc.Size = new Size(150, 49);
            streamModeDesc.TabIndex = 4;
            // 
            // radioBalanced
            // 
            radioBalanced.AutoSize = true;
            radioBalanced.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            radioBalanced.Location = new Point(120, 150);
            radioBalanced.Name = "radioBalanced";
            radioBalanced.Size = new Size(165, 46);
            radioBalanced.TabIndex = 5;
            radioBalanced.TabStop = true;
            radioBalanced.Text = "Balanced";
            radioBalanced.UseVisualStyleBackColor = true;
            // 
            // radioQuality
            // 
            radioQuality.AutoSize = true;
            radioQuality.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            radioQuality.Location = new Point(120, 250);
            radioQuality.Name = "radioQuality";
            radioQuality.Size = new Size(138, 46);
            radioQuality.TabIndex = 6;
            radioQuality.TabStop = true;
            radioQuality.Text = "Quality";
            radioQuality.UseVisualStyleBackColor = true;
            // 
            // radioStable
            // 
            radioStable.AutoSize = true;
            radioStable.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            radioStable.Location = new Point(120, 350);
            radioStable.Name = "radioStable";
            radioStable.Size = new Size(128, 46);
            radioStable.TabIndex = 7;
            radioStable.TabStop = true;
            radioStable.Text = "Stable";
            radioStable.UseVisualStyleBackColor = true;
            // 
            // radioMobile
            // 
            radioMobile.AutoSize = true;
            radioMobile.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            radioMobile.Location = new Point(120, 450);
            radioMobile.Name = "radioMobile";
            radioMobile.Size = new Size(130, 46);
            radioMobile.TabIndex = 8;
            radioMobile.TabStop = true;
            radioMobile.Text = "Mobile";
            radioMobile.UseVisualStyleBackColor = true;
            // 
            // Host
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(2287, 1014);
            Controls.Add(radioMobile);
            Controls.Add(radioStable);
            Controls.Add(radioQuality);
            Controls.Add(radioBalanced);
            Controls.Add(streamModeDesc);
            Name = "Host";
            Text = "TwinsRemote Host";
            Load += Host_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private TextBox streamModeDesc;
        private RadioButton radioBalanced;
        private RadioButton radioQuality;
        private RadioButton radioStable;
        private RadioButton radioMobile;
    }
}
