namespace TwinsRemoteHost
{
    partial class ConnectionSettings
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            okButton = new Button();
            cancelButton1 = new Button();
            screenOptionFlowLayoutPanel = new FlowLayoutPanel();
            selectScreenLabel = new Label();
            SuspendLayout();
            // 
            // okButton
            // 
            okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            okButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            okButton.Location = new Point(772, 1294);
            okButton.Name = "okButton";
            okButton.Size = new Size(131, 52);
            okButton.TabIndex = 8;
            okButton.TabStop = false;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            // 
            // cancelButton1
            // 
            cancelButton1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton1.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            cancelButton1.Location = new Point(621, 1294);
            cancelButton1.Name = "cancelButton1";
            cancelButton1.Size = new Size(131, 52);
            cancelButton1.TabIndex = 9;
            cancelButton1.TabStop = false;
            cancelButton1.Text = "Cancel";
            cancelButton1.UseVisualStyleBackColor = true;
            cancelButton1.Click += cancelButton1_Click;
            // 
            // screenOptionFlowLayoutPanel
            // 
            screenOptionFlowLayoutPanel.AutoScroll = true;
            screenOptionFlowLayoutPanel.Location = new Point(12, 50);
            screenOptionFlowLayoutPanel.Name = "screenOptionFlowLayoutPanel";
            screenOptionFlowLayoutPanel.Size = new Size(891, 724);
            screenOptionFlowLayoutPanel.TabIndex = 18;
            // 
            // selectScreenLabel
            // 
            selectScreenLabel.AutoSize = true;
            selectScreenLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            selectScreenLabel.ImageAlign = ContentAlignment.MiddleRight;
            selectScreenLabel.Location = new Point(12, 5);
            selectScreenLabel.Name = "selectScreenLabel";
            selectScreenLabel.RightToLeft = RightToLeft.Yes;
            selectScreenLabel.Size = new Size(98, 42);
            selectScreenLabel.TabIndex = 19;
            selectScreenLabel.Text = "label1";
            selectScreenLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ConnectionSettings
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(915, 1358);
            Controls.Add(selectScreenLabel);
            Controls.Add(cancelButton1);
            Controls.Add(okButton);
            Controls.Add(screenOptionFlowLayoutPanel);
            Name = "ConnectionSettings";
            Text = "ConnectionSettings";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private Button okButton;
        private Button cancelButton1;
        private FlowLayoutPanel monitorsFlowLayoutPanel1;
        private FlowLayoutPanel screenOptionFlowLayoutPanel;
        private FlowLayoutPanel monitorsFlowLayoutPanel2;
        private Label selectScreenLabel;
    }
}